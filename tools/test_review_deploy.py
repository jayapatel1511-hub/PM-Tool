"""AC-DLV-16, AC-DLV-17: fake commands prove deploy/rollback without touching Docker or homedev."""
import hashlib
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT/'hosting/pm-tool-review-deploy-root.sh').read_text().split("<<'DEPLOY_PY'\n",1)[1].split('\nDEPLOY_PY',1)[0]
NS = {'__name__':'test'}
exec(compile(SOURCE, 'deploy-helper', 'exec'), NS)


class ReviewDeployTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name)
        self.state=self.root/'state'; self.state.mkdir()
        for path in ['hosting/homedev.compose.yml','scripts/verify-homedev-review.py']:
            target=self.root/path;target.parent.mkdir(exist_ok=True);target.write_text('synthetic '+path)
        (self.root/'ghcr-token').write_text('secret-never-logged')
        self.digest='sha256:'+'d'*64;self.sha='a'*40
        self.labels={'org.opencontainers.image.revision':self.sha,'hub.contract-migration':'none'}
        for name,path in [('compose','hosting/homedev.compose.yml'),('probe','scripts/verify-homedev-review.py')]:
            self.labels['hub.review-'+name+'-sha256']=hashlib.sha256((self.root/path).read_bytes()).hexdigest()
        self.calls=[];self.reject='';self.failing_probe=False;self.current='b'*40;self.images='';self.repo_tags={}

    def fake_command(self,argv,**kw):
        self.calls.append(argv)
        if argv[0]=='gh' and self.reject=='provenance':raise RuntimeError('provenance failed')
        if 'imagetools' in argv:
            return json.dumps({'digest':self.digest}) if argv[-1]=='{{json .Manifest}}' else json.dumps({'config':{'Labels':self.labels}})
        if argv[:3]==['docker','image','ls']:return self.images
        if argv[:3]==['docker','image','inspect'] and argv[4]=='{{json .RepoTags}}': return json.dumps(self.repo_tags.get(argv[-1], []))
        if argv[:2]==['docker','inspect']:
            if argv[3]=='{{json .RepoTags}}': return json.dumps(self.repo_tags.get(argv[-1], []))
            return 'pm-tool-review-db' if argv[-1].endswith('db-1') else 'sha256:'+'b'*64 if argv[3]=='{{.Image}}' else 'pm-tool-review:'+self.current
        if 'psql' in argv:return ''
        if argv[0].endswith('pm-tool-review-backup'):return 'Verified private dump: /private/dump'
        if 'up' in argv:self.current=kw['env']['RELEASE_SHA']
        if argv[0]=='python3' and self.failing_probe and self.current==self.sha:raise RuntimeError('probe failed')
        return ''

    def deploy(self):
        with patch('time.sleep'):
            NS['deploy'](self.root,self.state,self.fake_command)

    def test_deploy_dumps_then_starts_and_records(self):
        self.deploy()
        state=json.loads((self.state/'state.json').read_text())
        self.assertEqual(state['digest'],self.digest)
        backup=next(i for i,c in enumerate(self.calls) if c[0].endswith('pm-tool-review-backup'))
        start=next(i for i,c in enumerate(self.calls) if 'up' in c)
        self.assertLess(backup,start)
        self.assertLess(self.calls.index(['test','-x','/usr/local/libexec/pm-tool-review-backup']),next(i for i,c in enumerate(self.calls) if c[:2] in (['docker','tag'],['docker','pull'])))
        self.assertTrue(all('pilot' not in ' '.join(c) for c in self.calls))
        self.assertIn('--no-build',self.calls[start])
        self.assertEqual(os.stat(self.state/'state.json').st_mode&0o777,0o644)

    def test_failed_probe_rolls_back_and_digest_is_never_retried(self):
        self.failing_probe=True
        with self.assertRaisesRegex(RuntimeError,'probe failed'):self.deploy()
        self.assertEqual(self.current,'rollback-'+'b'*64)
        self.assertIn(self.digest,json.loads((self.state/'state.json').read_text())['failed'])
        self.calls=[];self.deploy()
        self.assertEqual(len(self.calls),1)
        self.failing_probe=False;self.digest='sha256:'+'e'*64;self.sha='c'*40
        self.labels['org.opencontainers.image.revision']=self.sha
        self.deploy()
        self.assertEqual(self.current,self.sha)
        self.assertEqual(json.loads((self.state/'state.json').read_text())['digest'],self.digest)

    def test_guards_prevent_pull_or_start(self):
        for kind in ['provenance','contract','compose','probe']:
            with self.subTest(kind=kind):
                labels=dict(self.labels);self.calls=[];self.reject=kind
                if kind=='contract':self.labels['hub.contract-migration']='20261018235959_Drop'
                if kind in ['compose','probe']:self.labels['hub.review-'+kind+'-sha256']='changed'
                with self.assertRaises(RuntimeError):self.deploy()
                self.assertFalse(any('pull' in c or 'up' in c or c[0].endswith('pm-tool-review-backup') for c in self.calls))
                self.labels=labels

    def test_success_prunes_only_old_deploy_images(self):
        image=lambda c:'sha256:'+c*64
        rows=[(image('9'),self.sha),(image('8'),'8'*40),(image('7'),'7'*40),(image('b'),'b'*40),(image('b'),'rollback-'+'b'*64),
              (image('6'),'manual'),(image('5'),'5'*40),(image('4'),'4'*40)]
        self.images='\n'.join(i+' '+t for i,t in rows)
        self.repo_tags.update({i:['pm-tool-review:'+tag] for i,tag in rows})
        self.repo_tags[image('5')]=['pm-tool-review:'+'5'*40, 'local/manual-rollback:keep']
        self.deploy()
        # Newest three kept; rollback, hand-named, and externally aliased images are never removed.
        self.assertEqual([c[-1] for c in self.calls if c[:3]==['docker','image','rm']],[image('4')])


if __name__=='__main__':unittest.main()
