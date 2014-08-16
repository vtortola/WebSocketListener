angular.module("terminalServer", ['ui.bootstrap','vtortola.ng-terminal'])

.value("websocketUrl", "ws://localhost:8009")

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

        var correlationId = 0;
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
            return deferred.promise.then(function () {
                deferred.done = true;
            });
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
            var one = [];
            for (var i = 0; i < oneListeners.length; i++) {
                var listener = oneListeners[i];
                if (listener.p(obj))
                    one.push(listener);
            }

            for (var i = 0; i < one.length; i++) {
                var listener = one[i];
                listener.q.resolve(obj);
                oneListeners.removeOne(listener);
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
        $connection.listenOnce(function (msg) { msg.correlationId && msg.correlationId == crrId; })
                   .then(function (msg) {
                       alert("Console created");
                   });
        $connection.send({
            type:"CreateTerminalRequest",
            terminalType: "cmd.exe",
            correlationId: crrId
        });
    };

    $connection.listen(function (msg) { return true; }, function (msg) {
        console.log(JSON.stringify(msg));
    });

    $connection.listen(function (msg) { return true; }, function (msg) {
        if (msg.type == "CreatedTerminalEvent") {
            var terminal = {
                type: msg.type,
                id: msg.terminalId,
                currentPath:msg.currentPath
            };
            $scope.terminals.push(terminal);
            terminal.remove = function () {
                var index = $scope.terminals.indexOf(terminal);
                return function () {
                    $scope.terminals.splice(index, 1);
                    $scope.$$phase || $scope.$apply();
                };
            }();
            
            $scope.$$phase || $scope.$apply();
        }
    });
}])

.controller("consoleController", ["$scope", "$connection", "$rootScope", function ($scope, $connection, $rootScope) {

    $scope.terminalId = "empty";
    var terminal = null;

    $scope.init = function (t) {
        terminal = t;
        terminal.type = t.currentPath;
        $scope.terminalId = t.id;
        setTimeout(function () {
            $scope.$broadcast('terminal-command', {
                command: "change-prompt",
                prompt: { path: t.currentPath }
            });
        }, 100);
    };
    
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
            terminal.type = msg.currentPath;

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
            terminalId: $scope.terminalId
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