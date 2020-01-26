namespace WebSocketComunic
{
    public static class SimpleHtmlClient
    {
        public const string HTML =
@"<!DOCTYPE html>
  <meta charset=""utf-8""/>
  <title>WebSocket</title>
  <script language=""javascript"" type=""text/javascript"">

  var wsUri = ""ws://localhost:8080/"";
        var output;
        var websocket;

        function init()
        {
            output = document.getElementById(""output"");
        }

        function configWebSocket()
        {
            websocket = new WebSocket(wsUri);
            websocket.onopen = function(evt) { onOpen(evt) };
            websocket.onclose = function(evt) { onClose(evt) };
            websocket.onmessage = function(evt) { onMessage(evt) };
            websocket.onerror = function(evt) { onError(evt) };
        }

        function onOpen(evt)
        {
            emit(""CLI OPEN"");
            emit(""-----------------------------------------------------------------------------------"");
        }

        function onClose(evt)
        {
            emit(""-----------------------------------------------------------------------------------"");
            emit(""CLI CLOSE"");
            emit(""-----------------------------------------------------------------------------------"");

        }

        function onMessage(evt)
        {
            emit('<span style=""color:blue;"">SERVIDOR: ' + evt.data + '</span>');
        }

        function onError(evt)
        {
            emit('<span style=""color:red;"">ERROR: ' + evt.data + '</span>');
        }


        function emit(message)
        {
            var pre = document.createElement(""p"");
            pre.style.wordWrap = ""break-word"";
            pre.innerHTML = message;
            output.appendChild(pre);
        }

        function clickSend()
        {
            var txt = document.getElementById(""newMessage"");
            if (txt.value.length > 0)
            {
                sendTextFrame(txt.value);
                txt.value = """";
                txt.focus();
            }
        }

        function clickClose()
        {
            if (websocket.readyState == WebSocket.OPEN)
            {
                websocket.close();
            }
            else
            {
                emit(""Socket not open, state: "" + websocket.readyState);
            }

            document.getElementById(""Open"").disabled = false;
            document.getElementById(""closer"").disabled = true;
            document.getElementById(""newMessage"").disabled = true;
        }

        function clickOpen()
        {    
                websocket = new WebSocket(wsUri);
                websocket.onopen = function(evt) { onOpen(evt) };
                websocket.onclose = function(evt) { onClose(evt) };
                websocket.onmessage = function(evt) { onMessage(evt) };
                websocket.onerror = function(evt) { onError(evt) };

                document.getElementById(""Open"").disabled = true;
                document.getElementById(""closer"").disabled = false;
                document.getElementById(""newMessage"").disabled = true;
        }

        window.addEventListener(""load"", init, false);

  </script>

  <h2>Web Socket</h2>

  <p>
    <input style=""color:green;"" type=""button"" id=""Open"" value=""Conectar"" onclick=""clickOpen()""/>
    <input style=""color:orange;"" type=""button"" id=""closer"" value=""Desconectar"" onclick=""clickClose()""/>
  <div id= ""output""></div> 
";
    }
}
