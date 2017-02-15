var chat = function (control, websocket, notificationSound) {
    var me = {};
    var ws = new WebSocket(websocket, "chat");
    var nickInput = control.getElementsByClassName('nick-input')[0];
    var chatRoomInput = control.getElementsByClassName('chat-room-input')[0];
    var joinRoom = control.getElementsByClassName('join-room')[0];
    var chatLog = control.getElementsByClassName('chat-log')[0];
    var message = control.getElementsByClassName('message')[0];
    var send = control.getElementsByClassName('send')[0];
    var chatParticipants = control.getElementsByClassName('chat-participants')[0];
    var audioSwitch = control.getElementsByClassName('audio-switch')[0];
    audioSwitch.isAudioEnabled = true;

    message.disabled = true;
    send.disabled = true;
    joinRoom.disabled = true;
    var playNotification = function () { };
    
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
        sendMessage();
    });

    audioSwitch.addEventListener('click', function () {
        if (audioSwitch.isAudioEnabled) {
            audioSwitch.className='audio-switch disabled';
            audioSwitch.isAudioEnabled = false;
        }
        else {
            audioSwitch.className = 'audio-switch';
            audioSwitch.isAudioEnabled = true;
        }
    });

    message.addEventListener('keypress', function (e) {
        var code = (e.keyCode ? e.keyCode : e.which);
        if (code == 13)
            sendMessage();
    });

    var sendMessage = function () {
        if (message.value) {
            ws.send(JSON.stringify({ message: message.value, cls: "msg", room: chatRoomInput.value }));
            message.value = '';
        }
    };

    var addChatMessage = function (nick, message, timestamp) {
        var node = document.createElement('div');
        node.className = 'chat-msg';
        if(nick == me.nick)
            node.className += ' me';
        else if (nick != 'Server')
            playNotification();

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

    var handleJsonEvent = function (json) {
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

    var loadNotificationSound = function () {
        var request = new XMLHttpRequest();
        request.open('GET', notificationSound, true);
        request.responseType = 'arraybuffer';
        request.onload = function () {
            window.AudioContext = window.AudioContext || window.webkitAudioContext;
            var context = new AudioContext();
            context.decodeAudioData(request.response, function (buffer) {

                playNotification = function () {
                    if (audioSwitch.isAudioEnabled) {
                        var source = context.createBufferSource();
                        source.buffer = buffer;
                        source.connect(context.destination);
                        source.start(0);
                    }
                };
            });
        }
        request.send();
    };

    ws.onopen = function () {
        joinRoom.disabled = false;
        nickInput.disabled = false;
        chatRoomInput.disabled = false;
    };

    ws.onmessage = function (msg) {
        if (typeof msg.data == 'string') {
            handleJsonEvent(JSON.parse(msg.data));
        }
        else if (typeof msg.data == 'blob') {
            
        }
    };

    ws.onclose = function () {
        joinRoom.disabled = true;
        send.disabled = true;
        message.disabled = true;
    };

    loadNotificationSound();

    return me;
}


chats = [];
var elements = document.getElementsByClassName('chat');
for (var i = 0; i < elements.length; i++) {
    var node = elements[i];
    chats.push(chat(node, node.dataset.chatWebsocket, node.dataset.chatWebsocketNotification));
}
