"""Offline synthetic web fixture. Never serves a copyrighted voice recording."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import json,sys
frame=bytes.fromhex('fffb9000')+bytes(413)
media=frame*1024
class Handler(BaseHTTPRequestHandler):
 def do_GET(self):
  path=self.path.split('?')[0]
  with open(sys.argv[2],'a',encoding='utf-8') as f:f.write(path+'\n')
  if path=='/':body=b'''<script src="new-voices.js"></script> <script>throw Error('DO NOT EXECUTE'); const bad="https://outside.invalid/secret.mp3";</script>'''
  elif path=='/new-voices.js':body=b'''const samples=[{url:"audio/extra.mp3"}]; const hostile="http://169.254.169.254/secret.mp3"; const bad="/audio/not-audio.mp3";'''
  elif path in ['/service-worker.js','/voice-calibration.js','/voice-playback-policy.js']:body=b'/* static data only */'
  elif path=='/audio/not-audio.mp3':body=b'<html>'+b'not audio'*50+b'</html>'
  elif path.startswith(('/voices_pa3/','/voices_mmm/','/voices_v63/','/audio/extra.mp3')):body=media
  else:self.send_error(404);return
  self.send_response(200);self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
 def log_message(self,*args):pass
server=ThreadingHTTPServer(('127.0.0.1',0),Handler)
Path(sys.argv[1]).write_text(str(server.server_address[1]))
server.serve_forever()
