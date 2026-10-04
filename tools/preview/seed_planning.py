#!/usr/bin/env python3
"""Idempotent, permissioned packet-034 examples for the guarded synthetic Tuesday preview."""
import datetime as dt
import json
import urllib.request
from preview_target import verify_preview_target

BASE = 'http://localhost:5080'
verify_preview_target(BASE)
def call(path, who, method='GET', body=None):
    request = urllib.request.Request(BASE + '/api/v1/' + path, method=method, data=None if body is None else json.dumps(body).encode(), headers={'X-Dev-User':who+'@hub.test','Content-Type':'application/json'})
    with urllib.request.urlopen(request, timeout=30) as response:
        raw=response.read()
        return json.loads(raw) if raw else None
assert call('config','jordan')['authMode']=='Development'
users={u['email'].split('@')[0]:u['id'] for u in call('users','jordan')}
today=dt.date.fromisoformat(call('me','sam')['settings']['today'])
week=today-dt.timedelta(days=today.weekday())
for who, person, label, hours, confidence, visibility, source in [
 ('sam','alex','Synthetic preview — preliminary proposal effort for long-label review and weekly capacity discussion',30,'Confirmed','Published','Proposal'),
 ('sam','alex','Synthetic preview — professional training',13,'Expected','Published','Training'),
 ('sam','alex','Synthetic preview — private possible supervision support',8,'Possible','Draft','Supervision'),
 ('alex','alex','Synthetic preview — self-entered internal initiative',4,'Expected','Confirmed','InternalInitiative'),
 ('jordan','rita','Synthetic preview — read-only assignment context',6,'Possible','Published','OtherProject'),
 ('priya','priya','Synthetic preview — self-entered administration',4,'Confirmed','Confirmed','Admin'),
 ('sam','sam','Synthetic preview — self-entered field work',3,'Expected','Confirmed','FieldWork'),
]:
    existing=call('planning/entries?mine=true&pageSize=200',who)['items']
    if any(e['label']==label for e in existing): continue
    call('planning/entries',who,'POST',{'personId':users[person],'hoursPerWeek':hours,'startWeek':week.isoformat(),'endWeek':(week+dt.timedelta(days=21)).isoformat(),'label':label,'sourceCategory':source,'confidence':confidence,'visibility':visibility,'notes':'Synthetic preview fixture; fictional hours and work only.'})
print('Synthetic planning examples ready; no real records were used.')
