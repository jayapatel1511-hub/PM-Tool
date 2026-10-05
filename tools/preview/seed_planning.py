#!/usr/bin/env python3
"""Idempotent, permissioned packet-034 examples for the guarded synthetic Tuesday preview."""
import datetime as dt
from fixture_api import connect
from fixture_copy import natural_copy

client = connect()
BASE = client.base
call = client.call
users={u['email'].split('@')[0]:u['id'] for u in call('users','jordan')}
today=dt.date.fromisoformat(call('me','sam')['settings']['today'])
week=today-dt.timedelta(days=today.weekday())
for who, person, label, hours, confidence, visibility, source in [
 ('sam','alex','preliminary proposal effort for long-label review and weekly capacity discussion',30,'Confirmed','Published','Proposal'),
 ('sam','alex','professional training',13,'Expected','Published','Training'),
 ('sam','alex','private possible supervision support',8,'Possible','Draft','Supervision'),
 ('alex','alex','self-entered internal initiative',4,'Expected','Confirmed','InternalInitiative'),
 ('jordan','rita','read-only assignment context',6,'Possible','Published','OtherProject'),
 ('priya','priya','self-entered administration',4,'Confirmed','Confirmed','Admin'),
 ('sam','sam','self-entered field work',3,'Expected','Confirmed','FieldWork'),
]:
    existing=call('planning/entries?mine=true&pageSize=200',who)['items']
    if any(natural_copy(e['label'])==label for e in existing): continue
    call('planning/entries',who,'POST',{'personId':users[person],'hoursPerWeek':hours,'startWeek':week.isoformat(),'endWeek':(week+dt.timedelta(days=21)).isoformat(),'label':label,'sourceCategory':source,'confidence':confidence,'visibility':visibility,'notes':'Coordinate weekly work and capacity with the team.'})
print('Synthetic planning examples ready; no real records were used.')
