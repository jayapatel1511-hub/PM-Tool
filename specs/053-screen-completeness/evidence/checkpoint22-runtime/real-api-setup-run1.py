from pathlib import Path
import subprocess,os,json,time
root=Path(__file__).resolve().parents[4];out=Path(__file__).resolve().parent
env=os.environ.copy();env.update(TUESDAY_QA_DATABASE='hub_qa_checkpoint22',TUESDAY_QA_REQUIRE_CAPACITY_SCREENS='1')
results=[]
for name in ['timeline','deliverable','person']:
 started=time.time()
 with (out/('real-'+name+'-setup-run1.log')).open('w') as log:
  result=subprocess.run(['node','specs/053-screen-completeness/evidence/checkpoint22-real-'+name+'-setup.cjs'],cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
 row={'name':name,'exitCode':result.returncode,'seconds':round(time.time()-started,2),'log':'real-'+name+'-setup-run1.log'}
 results.append(row);(out/'real-api-setup-results1.json').write_text(json.dumps({'status':'complete' if len(results)==3 else 'running','results':results},indent=2)+'\n');print(json.dumps(row),flush=True)
raise SystemExit(0 if all(row['exitCode']==0 for row in results) else 1)
