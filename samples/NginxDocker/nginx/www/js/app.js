$(function(){

    var $input = $("#input");
    var $log = $("#log");
    var $send = $("#send");

    function padLeft(value, char, length){
        while(value.length < length)
            value = char + value;
        return value;
    }

    function asDigit(value){
        return padLeft(value, "0", 2);
    }

    function addLog(msg, msgClass){
        var now = new Date();
        var ts = asDigit(now.getHours().toString())+":"+asDigit(now.getMinutes().toString())+":"+asDigit(now.getSeconds().toString()) + " - ";
        $("<div/>")
            .text(ts + msg)
            .addClass("message")
            .addClass(msgClass)
            .appendTo($log);
        $log.scrollTop($log.prop("scrollHeight"));
    }

    window.socket = new WebSocket("wss://localhost:8721/socket");
    window.socket.onopen = function(){
        addLog("Connected", "connection");
    }
    window.socket.onclose = function(){
        addLog("Disconnected", "connection");
    }
    window.socket.onerror = function(e){
        addLog("Error: " + e.type, "error");
    }
    window.socket.onmessage = function(e){
        addLog("<< " + e.data, "in");
    }
    $send.bind("click", function(){
        var msg = $input.val();
        window.socket.send(msg);
        addLog(">> " + msg, "out");
    });
});

