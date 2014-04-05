var chat = function (control, url) {
    var me = {};
    var ws = new WebSocket(url, "chat");
    var nickInput = control.getElementsByClassName('nick-input')[0];
    var chatRoomInput = control.getElementsByClassName('chat-room-input')[0];
    var joinRoom = control.getElementsByClassName('join-room')[0];
    var chatLog = control.getElementsByClassName('chat-log')[0];
    var message = control.getElementsByClassName('message')[0];
    var send = control.getElementsByClassName('send')[0];
    var chatParticipants = control.getElementsByClassName('chat-participants')[0];

    message.disabled = true;
    send.disabled = true;
    joinRoom.disabled = true;

    joinRoom.addEventListener('click', function () {

        if (nickInput.value && chatRoomInput.value) {

            joinRoom.disabled = true;
            ws.send(JSON.stringify({ cls: "join", room: chatRoomInput.value, nick: nickInput.value }));
        }
        else {
            if (!nickInput.value || nickInput.value.length < 4) {
                if(nickInput.className.indexOf('error') == -1)
                    nickInput.className += ' error';
            }
            if (!chatRoomInput.value || nickInput.value.length < 4) {
                if (chatRoomInput.className.indexOf('error') == -1)
                    chatRoomInput.className += ' error';
            }
        }

    });

    send.addEventListener('click', function () {
        ws.send(JSON.stringify({ message: message.value, cls: "msg", room: chatRoomInput.value }));
    });

    var addChatMessage = function (nick, message, timestamp) {
        var node = document.createElement('div');
        node.className = 'chat-msg' +(nick == me.nick?' me':'');
        node.textContent = timestamp + ': [' + nick + ']->   ' + message;
        chatLog.appendChild(node);
        chatLog.scrollTop = chatLog.scrollHeight;
    };

    var addParticipant = function (nick) {
        var node = document.createElement('div');
        node.className = 'participant ' + nick;
        node.textContent = nick;
        chatParticipants.appendChild(node);
    };

    var removeParticipant = function (nick) {
        var goner = chatParticipants.getElementsByClassName('participant ' + nick);
        if (goner && goner.length) {
            chatParticipants.removeChild(goner[0]);
        }
    };

    ws.onopen = function () {
        joinRoom.disabled = false;
        nickInput.disabled = false;
        chatRoomInput.disabled = false;
    };

    ws.onmessage = function (msg) {
        var json = JSON.parse(msg.data);
        switch (json.cls) {
            case 'join':
                joinRoom.disabled = true;
                send.disabled = false;
                message.disabled = false;
                nickInput.disabled = true;
                chatRoomInput.disabled = true;
                addParticipant(json.nick);
                me.nick = json.nick;
                for (var i = 0; i < json.participants.length; i++) {
                    addParticipant(json.participants[i]);
                }
                break;
            case 'msg':
                addChatMessage(json.nick, json.message, json.timestamp);
                break;
            case 'joint':
                addParticipant(json.nick);
                break;
            case 'leave':
                removeParticipant(json.nick);
                break; 
        }
    };

    ws.onclose = function () {
        joinRoom.disabled = true;
        send.disabled = true;
        message.disabled = true;
    };

    return me;
}


chats = [];
var elements = document.getElementsByClassName('chat');
for (var i = 0; i < elements.length; i++) {
    var node = elements[i];
    chats.push(chat(node, node.dataset.websocket));
}
