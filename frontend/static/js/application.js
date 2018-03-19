$(document).ready(function(){
    //connect to the socket server.
    var socket = io.connect('http://' + document.domain + ':' + location.port);

    //receive details from server
    socket.on('newevent', function(msg) {
        $('#log').append('<p>' + msg.message + '</p>');
    });

});