angular.module("terminalServer", ['ui.bootstrap','vtortola.ng-terminal'])

.service("guid", function () {
    var guid = (function () {
        function s4() {
            return Math.floor((1 + Math.random()) * 0x10000)
                       .toString(16)
                       .substring(1);
        }
        return function () {
            return s4() + s4() + '-' + s4() + '-' + s4() + '-' +
                   s4() + '-' + s4() + s4() + s4();
        };
    })();
    return guid;
})

.factory("websocketUrl",["guid", function (guid) {
    
    var url = "ws://localhost:8006/" + guid();
    if (window.sessionStorage) {
        var stored = window.sessionStorage.getItem("websocketUrl");
        if (stored) {
            console.log("Using stored connection: " + stored);
            return stored;
        }
        else
            window.sessionStorage.setItem("websocketUrl",url);
    }
    console.log("Using new connection: " + url);
    return url;
}])

.service('$connection', ["$q", "$timeout", "websocketUrl", "$rootScope", function ($q, $timeout, websocketUrl, $rootScope) {
    var connection = function () {

        var me = {};
        var listeners = [];
        var oneListeners = [];

        me.isConnected = false;

        oneListeners.removeOne = function (listener) {
            var index = oneListeners.indexOf(listener);
            if(index!=-1)
                oneListeners.splice(index, 1);
        };

        var correlationId = 1;
        me.nextCorrelationId = function () {
            return correlationId++;
        };

        $rootScope.queuedMessages = [];

        me.listen = function (predicate, handler) {
            listeners.push({ p: predicate, h: handler });
        };

        me.listenOnce = function (predicate, timeout) {
            var deferred = $q.defer();
            deferred.done = false;
            var listener = { d: deferred, p: predicate };
            oneListeners.push(listener);
            if (timeout) {
                $timeout(function () {
                    if (!deferred.done)
                        deferred.reject('timeout');
                    oneListeners.removeOne(listener);
                }, timeout);
            }
            var promise = deferred.promise;
            promise.then(function (data) {
                deferred.done = true;
            });
            return promise;
        };

        var onopen = function () {
            console.log('onopen');
            $rootScope.websocketAvailable = true;
            me.isConnected = true;
            $rootScope.$$phase || $rootScope.$apply();
            if ($rootScope.queuedMessages) {
                for (var i = 0; i < $rootScope.queuedMessages.length; i++) {
                    ws.send(JSON.stringify($rootScope.queuedMessages[i]));
                }
                $rootScope.queuedMessages = null;
                $rootScope.$$phase || $rootScope.$apply();
            }
        };

        var onclose = function () {
            console.log('onclose');
            me.isConnected = false;
            $rootScope.websocketAvailable = false;
            $rootScope.$$phase || $rootScope.$apply();
            $rootScope.queuedMessages = $rootScope.queuedMessages || [];
            
            setTimeout(function () {
                ws = connect();
            }, 5000);
        };

        var onmessage = function (msg) {
            console.log('onmessage');
            var obj = JSON.parse(msg.data);
            for (var i = 0; i < listeners.length; i++) {
                var listener = listeners[i];
                if (listener.p(obj))
                    listener.h(obj);
            }
            var remove = [];
            for (var i = 0; i < oneListeners.length; i++) {
                var listener = oneListeners[i];
                if (listener.p(obj)) {
                    var o = obj;
                    listener.d.resolve(o);
                    remove.push(listener);
                }
            }
            for (var i = 0; i < remove.length; i++) {
                oneListeners.removeOne(remove[i]);
            }
        };

        var onerror = function () {
            console.log('onerror');
        };

        me.send = function (obj) {
            
            if ($rootScope.queuedMessages)
                $rootScope.queuedMessages.push(obj);
            else
                ws.send(JSON.stringify(obj));
        }

        var setHandlers = function (w) {
            w.onopen = onopen;
            w.onclose = onclose;
            w.onmessage = onmessage;
            w.onerror = onerror;
        };

        var connect = function () {
            console.log('connecting...');
            var w = new WebSocket(websocketUrl);
            setHandlers(w);
            return w;
        }

        var ws = connect();

        return me;
    };
    return connection();
}])

.controller("mainController", ["$scope", "$connection","$rootScope", function ($scope, $connection, $rootScope) {

    $rootScope.alerts = [];
    $rootScope.closeAlert = function (index) {
        $rootScope.alerts.splice(index, 1);
    };
    $rootScope.addAlert = function (message) {
        $rootScope.alerts.push(message);
    }
    $rootScope.checkConnection = function () {
        if (!$connection.isConnected) {
            $rootScope.addAlert({ msg: "WebSocket is not connected.", type: "danger" });
            return false;
        }
        return true;
    };

    $scope.terminals = [];

    $scope.createConsole = function (type) {
        if (!$rootScope.checkConnection()) {
            return;
        }
        var crrId = $connection.nextCorrelationId();
        $connection.listenOnce(function (data) {
            return data.correlationId && data.correlationId == crrId;
        }).then(function (data) {
            $rootScope.addAlert({ msg: "Console " + data.terminalType + " created", type: "success" });
        });
        $connection.send({
            type:"CreateTerminalRequest",
            terminalType: type,
            correlationId: crrId
        });
    };

    $connection.listen(function (msg) { return true; }, function (msg) {
        console.log(JSON.stringify(msg));
    });

    var addTerminal = function (descriptor) {
        var terminal = {
            terminalType: descriptor.terminalType,
            id: descriptor.terminalId,
            currentPath: descriptor.currentPath
        };
        $scope.terminals.push(terminal);
        terminal.remove = function () {
            var index = $scope.terminals.indexOf(terminal);
            return function () {
                $scope.terminals.splice(index, 1);
                $scope.$$phase || $scope.$apply();
            };
        }();
    }

    $connection.listen(function (msg) { return msg.type == "CreatedTerminalEvent"; }, 
        function (msg) {
            addTerminal(msg);
            $scope.$$phase || $scope.$apply();
        });
    $connection.listen(function (msg) { return msg.type == "SessionStateEvent"; },
        function (msg) {

            var disconnected = $scope.terminals.filter(function (item) {
                return !msg.terminals || !msg.terminals.some(function (item2) {
                    return item2.terminalId == item.terminalId;
                });
            });
            if (disconnected.length) {
                disconnected.forEach(function (item) {
                    var index = $scope.terminals.indexOf(item);
                    $scope.terminals.splice(index, 1);
                });
                $rootScope.addAlert({ msg: "Some terminals have been removed because your session expired.", type: "info" });
            }
            
            if (msg.terminals) {
                msg.terminals.forEach(function (item) {
                    addTerminal(item);
                });
            }
            $scope.$$phase || $scope.$apply();
        });
}])

.controller("consoleController", ["$scope", "$connection", "$rootScope", function ($scope, $connection, $rootScope) {

    $scope.terminalId = "empty";
    var terminal = null;
    var timer = null;
    $scope.init = function (t) {
        console.log(t);
        terminal = t;
        terminal.type = t.terminalType;
        $scope.terminalId = t.id;
        $scope.tabHeader = terminal.type;
        setTimeout(function () {
            $scope.$broadcast('terminal-command', {
                command: "change-prompt",
                prompt: { path: t.currentPath }
            });
        }, 100);
    };
    var currentCommandResult = 0;
    var showPrompt = true;
    $connection.listen(function (msg) { return true; }, function (msg) {

        if (!msg.terminalId || msg.terminalId != $scope.terminalId)
            return;

        if (msg.type && msg.type == "ClosedTerminalEvent") {
            terminal.remove();
        }
        else if (msg.type && msg.type == "TerminalOutputEvent") {

            if (!$scope.selected)
                $scope.pendingOutput = true;

            $scope.$broadcast('terminal-output', {
                output: true,
                text: [msg.output]
            });

            $scope.$broadcast('terminal-command', {
                command: "change-prompt",
                prompt: { path: msg.currentPath }
            });
            $scope.tabHeader = terminal.type +  " ["+msg.currentPath.substr(0,20)+"]";

            if (msg.correlationId > currentCommandResult) {
                currentCommandResult = msg.correlationId;
                showPrompt = msg.endOfCommand;
            }
            else if (msg.endOfCommand) {
                showPrompt = true;
            }
            $scope.showPrompt = showPrompt;
            $scope.$$phase || $scope.$apply();
        }
    });

    $scope.pendingOutput = false;
    $scope.selected = false;

    $scope.select = function () {
        $scope.selected = true;
        $scope.pendingOutput = false;
    };

    $scope.deselect = function () {
        $scope.selected = false;
    };

    $scope.send = function (cmd) {
        if (!$rootScope.checkConnection())
            return;

        $connection.send({
            type: "TerminalInputRequest",
            input: cmd,
            terminalId: $scope.terminalId,
            correlationId: $connection.nextCorrelationId()
        });
    };

    $scope.close = function () {
        $connection.send({
            type: "CloseTerminalRequest",
            terminalId: $scope.terminalId
        });
       terminal.remove();
    };

    $scope.$on('terminal-input', function (e, consoleInput) {
        
        var cmd = consoleInput[0]
        $scope.send(cmd.command);
    });
}])

.config(['terminalConfigurationProvider', function (terminalConfigurationProvider) {
    terminalConfigurationProvider.promptConfiguration = { end:'>', user:'', separator:'', path:''};
}])

;