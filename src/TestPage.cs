namespace LocalBotServer
{
    // Pagina de teste servida a um navegador em http://127.0.0.1:<porta>/.
    public static class TestPage
    {
        public static string Html(int port)
        {
            return "<!DOCTYPE html>" +
                   "<html><head><meta charset='utf-8'><title>BotServer Test</title>" +
                   "<style>body{font-family:Segoe UI,Roboto,sans-serif;background:#1e1e2e;color:#cdd6f4;padding:30px;}" +
                   ".card{background:#313244;padding:24px;border-radius:10px;max-width:620px;margin:auto;box-shadow:0 6px 16px rgba(0,0,0,0.35);}" +
                   "h2{color:#a6e3a1;margin-top:0;font-size:22px;}" +
                   "p{font-size:14px;color:#bac2de;margin:8px 0;}" +
                   "code{background:#181825;padding:3px 8px;border-radius:4px;color:#89b4fa;font-family:monospace;}" +
                   ".btn{background:#89b4fa;color:#11111b;border:none;padding:11px 22px;border-radius:6px;font-weight:bold;cursor:pointer;font-size:14px;margin-top:10px;transition:0.2s;}" +
                   ".btn:hover{background:#b4befe;}" +
                   "#log{background:#181825;padding:15px;border-radius:6px;margin-top:16px;font-family:Consolas,monospace;white-space:pre-wrap;height:190px;overflow-y:auto;font-size:13px;border:1px solid #45475a;}" +
                   ".ok{color:#a6e3a1;}.err{color:#f38ba8;}" +
                   "</style></head><body>" +
                   "<div class='card'>" +
                   "<div style='background:#181825;padding:12px;border-radius:6px;margin-bottom:16px;border-left:4px solid #cba6f7;'>" +
                   "<div style='color:#f9e2af;font-weight:bold;font-size:15px;'>made by: vivodibra</div>" +
                   "<div style='color:#89dceb;margin-top:4px;'>Join Discord server for free scripts:</div>" +
                   "<a href='https://discord.gg/RkQ9nyPMBH' target='_blank' style='color:#cba6f7;text-decoration:none;font-weight:bold;display:inline-block;margin-top:4px;'>https://discord.gg/RkQ9nyPMBH</a>" +
                   "</div>" +
                   "<h2>&#10004; BotServer Online &amp; Running!</h2>" +
                   "<p>WebSocket Port: <b>" + port + "</b></p>" +
                   "<p>URL for bot in Lua: <code>ws://localhost:" + port + "/</code></p>" +
                   "<button class='btn' onclick='testWebSocket()'>Test WebSocket Connection Now</button>" +
                   "<div id='log'>Click the button above to test real-time WebSocket communication...</div>" +
                   "</div>" +
                   "<script>" +
                   "function testWebSocket(){" +
                   "  var log = document.getElementById('log');" +
                   "  log.innerHTML = 'Connecting to ws://' + window.location.host + '/...\\n';" +
                   "  var ws = new WebSocket('ws://' + window.location.host + '/');" +
                   "  ws.onopen = function(){" +
                   "    log.innerHTML += '<span class=\"ok\">[SUCCESS] WebSocket connected to BotServer!</span>\\n';" +
                   "    log.innerHTML += 'Sending auth packet init...\\n';" +
                   "    ws.send(JSON.stringify({type:\"init\", name:\"BrowserTestBot\", channel:\"Global\"}));" +
                   "    setTimeout(function(){" +
                   "      log.innerHTML += 'Sending test broadcast message...\\n';" +
                   "      ws.send(JSON.stringify({type:\"message\", topic:\"test\", message:\"Hello from Browser!\"}));" +
                   "    }, 400);" +
                   "  };" +
                   "  ws.onmessage = function(e){" +
                   "    log.innerHTML += '<span class=\"ok\">[RECEIVED] ' + e.data + '</span>\\n';" +
                   "  };" +
                   "  ws.onerror = function(err){" +
                   "    log.innerHTML += '<span class=\"err\">[ERROR] WebSocket connection failed.</span>\\n';" +
                   "  };" +
                   "  ws.onclose = function(){" +
                   "    log.innerHTML += 'Connection closed gracefully.\\n';" +
                   "  };" +
                   "}" +
                   "</script></body></html>";
        }
    }
}
