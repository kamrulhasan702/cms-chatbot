"""HTTP integration checks using a local fake AI provider, never a paid AI API."""
from pathlib import Path
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json,os,socket,subprocess,threading,time,urllib.request,urllib.error,http.cookiejar
ROOT=Path(__file__).resolve().parents[1]
def port():
 with socket.socket() as s:s.bind(('127.0.0.1',0));return s.getsockname()[1]
class Provider(BaseHTTPRequestHandler):
 calls=0
 def log_message(self,*args):pass
 def do_POST(self):
  Provider.calls+=1
  if self.headers.get('Transfer-Encoding','').lower()=='chunked':
   parts=[]
   while True:
    size=int(self.rfile.readline().strip().split(b';')[0],16)
    if size==0:self.rfile.readline();break
    parts.append(self.rfile.read(size));self.rfile.read(2)
   raw=b''.join(parts)
  else:raw=self.rfile.read(int(self.headers.get('Content-Length','0')))
  body=json.loads(raw)
  user=body['messages'][1]['content']
  is_image=isinstance(user,list)
  payload=json.loads(user[0]['text'] if is_image else user);q=payload.get('question','')
  if q=='simulate upstream busy':
   self.send_response(429);self.end_headers();self.wfile.write(b'{}');return
  if is_image:
   assert body['model']=='qwen/qwen3.8-27b'
   assert user[1]['type']=='image_url' and user[1]['image_url']['url'].startswith('data:image/png;base64,')
   content={'readable':q!='unclear screenshot','isCmsScreen':q!='landscape photo','observation':'On-Air Schedule form with date and time' if q!='unclear screenshot' else '', 'searchQuery':'schedule news'}
  elif 'evidence' not in payload:
   language='bn' if any('\u0980'<=c<='\u09ff' for c in q) else 'banglish' if 'korbo' in q else 'en'
   query='unicorn quantum astrophysics' if 'unicorn' in q else 'schedule news article future date time'
   content={'language':language,'searchQuery':query,'inScope':not any(x in q.lower() for x in ['facebook','politics','weather','unicorn','আবহাওয়া'])}
  else:
   content={'answerable':True,'answer':'Schedule select kore future date o time dao.' if payload['language']=='banglish' else 'শিডিউল নির্বাচন করে তারিখ ও সময় ঠিক করুন।' if payload['language']=='bn' else 'Select Schedule and choose a future date and time.', 'evidenceIds':[payload['evidence'][0]['id']]}
  if 'evidence' in payload and 'section heading' in q:
   content['answer']='Click Image, choose Search or Upload, set Caption and Alignment, then click Insert Image.'
  response={'choices':[{'finish_reason':'stop','message':{'content':json.dumps(content,ensure_ascii=False)}}]}
  self.send_response(200);self.send_header('Content-Type','application/json');self.end_headers();self.wfile.write(json.dumps(response).encode())
def run(dotnet):
 provider=ThreadingHTTPServer(('127.0.0.1',0),Provider)
 threading.Thread(target=provider.serve_forever,daemon=True).start();appport=port()
 env=os.environ|{'ASPNETCORE_ENVIRONMENT':'Production','ASPNETCORE_URLS':f'http://127.0.0.1:{appport}','Ai__ApiKey':'local-smoke-test','Ai__VisionModel':'qwen/qwen3.8-27b','Ai__BaseUrl':f'http://127.0.0.1:{provider.server_port}/v1/','Chat__PermitLimit':'50','Access__Password':'local-test-password-123'}
 dll=ROOT/'src/ManualChatbot/bin/Release/net10.0/ManualChatbot.dll'
 log=open('/tmp/manual-chatbot-smoke.log','w')
 process=subprocess.Popen([dotnet,str(dll)],cwd=ROOT/'src/ManualChatbot',env=env,stdout=log,stderr=subprocess.STDOUT)
 jar=http.cookiejar.CookieJar();client=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
 base=f'http://127.0.0.1:{appport}'
 def req(path,data=None):
  request=urllib.request.Request(base+path,data=json.dumps(data).encode() if data is not None else None,headers={'Content-Type':'application/json'})
  try:
   with client.open(request,timeout=15) as r:return r.status,r.read(),r.headers
  except urllib.error.HTTPError as e:return e.code,e.read(),e.headers
 try:
  for _ in range(100):
   if process.poll() is not None:raise RuntimeError(Path('/tmp/manual-chatbot-smoke.log').read_text())
   try:
    if req('/health')[0]==200:break
   except OSError:pass
   time.sleep(.1)
  else:raise RuntimeError('App did not start')
  status,html,headers=req('/');assert status==200 and b'<h1>Assistant</h1>' in html and b'Type your message' in html;assert b'id="newChat"' not in html and b'id="signOut"' not in html;assert "script-src 'self'" in headers['Content-Security-Policy']
  assert 'no-store' in headers.get('Cache-Control','')
  assert b'illustrated-r9' in html
  assert json.loads(req('/api/version')[1])['version']=='illustrated-r9'
  asset=req('/styles.css?v=illustrated-r9')
  assert asset[0]==200 and 'no-store' in asset[2].get('Cache-Control','')
  assert req('/styles.css')[0]==200
  figure=json.loads((ROOT/'src/ManualChatbot/Knowledge/figures.json').read_text())['images'][0]['id']
  assert req('/api/manual-images/'+figure)[0]==401
  assert req('/api/chat',{'message':'How to schedule news?'})[0]==401
  assert req('/api/signin',{'password':'wrong'})[0]==401
  assert req('/api/signin',{'password':'local-test-password-123'})[0]==200
  status,body,headers=req('/api/manual-images/'+figure)
  assert status==200 and headers['Content-Type']=='image/webp' and body[:4]==b'RIFF'
  assert req('/api/manual-images/fig-000000000000')[0]==404
  assert req('/api/chat',{'message':' '})[0]==400
  before=Provider.calls
  for question,language,prefix in [('Hi','en','Hi!'),('bye','en','Goodbye!'),('ok','en','Okay.'),('হ্যালো','bn','হ্যালো!'),('thik ache','banglish','Thik ache.')]:
   status,body,_=req('/api/chat',{'message':question});result=json.loads(body)
   assert status==200 and result['language']==language and not result['usedFallback'] and result['answer'].startswith(prefix),(question,result)
  assert Provider.calls==before,'Small talk called the AI provider'
  for question,language in [('How do I schedule news?','en'),('নিউজ শিডিউল কীভাবে করব?','bn'),('news schedule korbo kivabe?','banglish')]:
   status,body,_=req('/api/chat',{'message':question});result=json.loads(body)
   assert status==200,(status,body)
   assert result['language']==language and result['answer'] and not result['usedFallback']
   assert not any(x in result for x in ['sources','page','pageNumber'])
  before=Provider.calls
  status,body,_=req('/api/chat',{'message':'I NSE RT IMAGE FROM RICH-TEXT EDITOR'});result=json.loads(body)
  assert status==200 and not result['usedFallback'] and 'Insert Image' in result['answer'],result
  assert Provider.calls==before+2,'Exact section title uses answer generation and evidence review'
  assert len(result['images'])==3 and all(i['url'].startswith('/api/manual-images/fig-') for i in result['images'])
  status,body,_=req('/api/chat',{'message':'Tell me about unicorn quantum astrophysics'})
  assert status==200 and json.loads(body)['answer']=='I have no information about this.'
  for question,expected in [('How do I login to Facebook?','I have no information about this.'),('আজকের আবহাওয়া কী?','এই বিষয়ে আমার কাছে কোনো তথ্য নেই।'),('Facebook e login korbo kivabe?','Ei bishoye amar kache kono information nei.')]:
   status,body,_=req('/api/chat',{'message':question});result=json.loads(body)
   assert status==200 and result['usedFallback'] and result['answer']==expected,(question,result)
  assert req('/api/chat',{'message':'simulate upstream busy'})[0]==429
  image='data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII='
  status,body,_=req('/api/chat',{'message':'এখন কী করব?','imageDataUrl':image});result=json.loads(body)
  assert status==200 and result['language']=='bn' and not result['usedFallback'],(status,result)
  before=Provider.calls
  status,body,_=req('/api/chat',{'message':'look at screenshot','imageDataUrl':'https://example.com/x.png'})
  assert status==400 and Provider.calls==before,'Remote URL must be rejected without provider request'
  status,body,_=req('/api/chat',{'message':'unclear screenshot','imageDataUrl':image})
  assert status==200 and 'clearer screenshot' in json.loads(body)['answer']
  status,body,_=req('/api/chat',{'message':'landscape photo','imageDataUrl':image})
  assert status==200 and json.loads(body)['usedFallback']
  status,body,_=req('/api/chat',{'message':'Tell me weather','imageDataUrl':image})
  assert status==200 and json.loads(body)['usedFallback']
  assert req('/api/signout',{})[0]==200
  figure=json.loads((ROOT/'src/ManualChatbot/Knowledge/figures.json').read_text())['images'][0]['id']
  assert req('/api/manual-images/'+figure)[0]==401
  assert req('/api/chat',{'message':'How to schedule news?'})[0]==401
  print('HTTP smoke tests passed: UI/static files, CSP, access cookie, validation, three languages, fallback, provider failure and sign-out.')
 finally:
  process.terminate();process.wait(timeout=10);provider.shutdown();log.close()
if __name__=='__main__':
 import sys
 run(sys.argv[1] if len(sys.argv)>1 else 'dotnet')
