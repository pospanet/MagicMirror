// Write your Javascript code.

$(window).ready(function () {
    var mediaStream = null;
    var webcamList;
    var currentCam = null;

    var writeError = function (string) {
        alert(string);
    };

    var initializeVideoStream = function (stream) {
        mediaStream = stream;

        var video = document.getElementById('video');
        video.srcObject = mediaStream;

        if (video.paused) video.play();

        $("#capture").removeAttr("disabled");

        if (webcamList.length > 1) {
            $('#switchCamera').removeAttr("disabled");
        }
    };

    var getUserMediaError = function (e) {
        if (e.name.indexOf('NotFoundError') >= 0) {
            writeError('Webcam not found.');
        }
        else {
            writeError('The following error occurred: "' + e.name + '" Please check your webcam device(s) and try again.');
        }
    };

    var capture = function () {

        if (!mediaStream) {
            return;
        }

        var video = document.getElementById('video');
        var canvas = document.getElementById('canvas');
        var videoWidth = video.videoWidth;
        var videoHeight = video.videoHeight;

        if (canvas.width !== videoWidth || canvas.height !== videoHeight) {
            canvas.width = videoWidth;
            canvas.height = videoHeight;
        }

        var ctx = canvas.getContext('2d');
        ctx.drawImage(video, 0, 0, video.videoWidth, video.videoHeight);

    };

    var nextWebCam = function () {
        $('#switchCamera').attr('disabled', 'disabled');
        if (currentCam !== null) {
            currentCam++;
            if (currentCam >= webcamList.length) {
                currentCam = 0;
            }
            var video = document.getElementById('video');
            if (typeof (video.srcObject) !== 'undefined') video.srcObject = null;
            video.src = null;
            if (mediaStream) {
                var videoTracks = mediaStream.getVideoTracks();
                videoTracks[0].stop();
                mediaStream = null;
            }
        }
        else {
            currentCam = 0;
        }

        navigator.mediaDevices.getUserMedia({
            video: {
                width: 640,
                height: 360,
                deviceId: { exact: webcamList[currentCam] }
            }
        }).then(initializeVideoStream).catch(getUserMediaError);
    };

    var enumerateMediaDevices = function () {
        navigator.mediaDevices.enumerateDevices().then(devicesCallback).catch(getUserMediaError);
    };

    var deviceChanged = function () {
        navigator.mediaDevices.removeEventListener('devicechange', deviceChanged);

        webcamList = [];
        enumerateMediaDevices();
    };

    var devicesCallback = function (devices) {
        // Identify all webcams
        webcamList = [];
        for (var i = 0; i < devices.length; i++) {
            if (devices[i].kind === 'videoinput') {
                webcamList[webcamList.length] = devices[i].deviceId;
            }
        }

        if (webcamList.length > 0) {
            // Start video with the first device on the list
            nextWebCam();
            if (webcamList.length > 1) {
                $('#switchCamera').removeAttr("disabled");;
            }
            else {
                $('#switchCamera').attr("disabled", "disabled");
            }
        }
        else {
            writeError('Webcam not found.');
        }
        navigator.mediaDevices.addEventListener('devicechange', deviceChanged);
    };

    var init = function () {
        if (navigator.getUserMedia) {
            enumerateMediaDevices();

            $('#capture').on('click', capture);
            $('#switchCamera').on('click', nextWebCam);
            $("#checkFace").on("click", checkFace);
            $("#linkFace").on("click", linkFace);
            $("#deleteIdentity").on("click", deleteIdentity);
        }
        else {
            writeError('You are using a browser that does not support the Media Capture API');
        }
    };

    init();

    function checkFace() {
        $("#checkFace").attr('disabled', 'disabled');
        $("#checkFace").attr("value", "Uploading...");
        var img = canvas.toDataURL('image/jpeg', 0.9).split(',')[1];
        $.ajax({
            url: "ajax/checkFace",
            type: "POST",
            data: JSON.stringify({ image: img }),
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: function (data) {
                alert(JSON.stringify(data));
                $("#checkFace").removeAttr('disabled');
                $("#checkFace").attr("value", "Check face");
            },
            error: function () {
                alert("There was some error while uploading Image");
                $("#checkFace").removeAttr('disabled');
                $("#checkFace").attr("value", "Check face");
            }
        });
    }
    function linkFace() {
        $("#linkFace").attr('disabled', 'disabled');
        $("#linkFace").attr("value", "Uploading...");
        var img = canvas.toDataURL('image/jpeg', 0.9).split(',')[1];
        $.ajax({
            url: "ajax/linkFace",
            type: "POST",
            data: JSON.stringify({ image: img }),
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: function (data) {
                if (data.error) {
                    alert(data.error);
                }
                else {
                    alert(JSON.stringify(data));
                }
                $("#linkFace").removeAttr('disabled');
                $("#linkFace").attr("value", "Link face");
            },
            error: function () {
                alert("There was some error while uploading image");
                $("#linkFace").removeAttr('disabled');
                $("#linkFace").attr("value", "Link face");
            }
        });
    }
    function deleteIdentity() {
        $("#linkFace").attr('disabled', 'disabled');
        $("#linkFace").attr("value", "Deleting...");
        var img = canvas.toDataURL('image/jpeg', 0.9).split(',')[1];
        $.ajax({
            url: "ajax/deleteIdentity",
            type: "POST",
            data: JSON.stringify({ image: img }),
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: function (data) {
                alert(JSON.stringify(data));
                $("#linkFace").removeAttr('disabled');
                $("#linkFace").attr("value", "Delete identity");
            },
            error: function () {
                alert("There was some error while deleting identity");
                $("#linkFace").removeAttr('disabled');
                $("#linkFace").attr("value", "Delete identity");
            }
        });
    }
}, false);