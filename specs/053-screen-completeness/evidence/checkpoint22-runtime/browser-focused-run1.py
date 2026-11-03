from pathlib import Path
import subprocess,os,json,time
root=Path(__file__).resolve().parents[4]
out=Path(__file__).resolve().parent
env=os.environ.copy()
env.update(TUESDAY_UI_BASE_URL='http://127.0.0.1:8853',CHROME_EXECUTABLE_PATH='/Applications/Google Chrome.app/Contents/MacOS/Google Chrome')
suites=['count-card-overlap','timeline-saved-views','person-panel','deliverable-register']
results=[]
for suite in suites:
 started=time.time()
 with (out/(suite+'-run1.log')).open('w') as log:
  result=subprocess.run(['node','tests/'+suite+'.cjs'],cwd=root/'web',env=env,stdout=log,stderr=subprocess.STDOUT)
 row={'suite':suite,'exitCode':result.returncode,'seconds':round(time.time()-started,2),'log':suite+'-run1.log'}
 results.append(row)
 (out/'browser-focused-results1.json').write_text(json.dumps({'status':'complete' if len(results)==len(suites) else 'running','results':results},indent=2)+'\n')
 print(json.dumps(row),flush=True)
raise SystemExit(0 if all(row['exitCode']==0 for row in results) else 1)
