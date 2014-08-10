angular.module("terminalServer", ['ui.bootstrap','vtortola.ng-terminal'])

.value("websocketUrl", "ws://localhost:8008")

.service('$connection', ["$q", "$timeout", "websocketUrl", "$rootScope", function ($q, $timeout, websocketUrl, $rootScope) {
    var connection = function () {

        var me = {};
        var listeners = [];
        var oneListeners = [];
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
            $rootScope.websocketAvailable = false;
            $rootScope.$$phase || $rootScope.$apply();
            $rootScope.queuedMessages = $rootScope.queuedMessages || [];
            
            //ws = connect();
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

.controller("mainController", ["$scope", "$connection", function ($scope, $connection) {

    $scope.terminals = [];

    $scope.createConsole = function (type) {
        var crrId = $connection.nextCorrelationId();
        $connection.listenOnce(function (msg) { msg.correlationId && msg.correlationId == crrId; })
                   .then(function (msg) {
                       
                   });
        $connection.send({
            label: "terminal-control-request",
            command: "create-terminal",
            type: "command",
            correlationId: crrId
        });
    };

    $connection.listen(function (msg) { return true; }, function (msg) {
        console.log(JSON.stringify(msg));
    });

    $connection.listen(function (msg) { return true; }, function (msg) {
        if (msg.command == "terminal-created-event") {
            $scope.terminals.push({
                type: msg.type,
                id: msg.terminalId
            });
            $scope.$$phase || $scope.$apply();
        }
    });
}])

.controller("consoleController", ["$scope", "$connection", function ($scope, $connection) {

    $scope.terminalId = "empty";
    $scope.init = function (tid) {
        $scope.terminalId = tid;
    };

    $connection.listen(function (msg) { return true; }, function (msg) {
        if (msg.output && msg.terminalId && msg.terminalId == $scope.terminalId) {
            
            $scope.$broadcast('terminal-output', {
                output: true,
                text: [msg.output]
            });

            $scope.$$phase || $scope.$apply();
        }
    });

    $scope.send = function (cmd) {
        $connection.send({
            label: "terminal-control-request",
            command: "terminal-input",
            input: cmd,
            terminalId: $scope.terminalId
        });
    };

    $scope.$on('terminal-input', function (e, consoleInput) {
        var cmd = consoleInput[0]
        $scope.send(cmd.command);
    });
}])

.config(['terminalConfigurationProvider', function (terminalConfigurationProvider) {

}])

;