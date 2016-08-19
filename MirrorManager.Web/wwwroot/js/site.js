// Write your Javascript code.

window.addEventListener("DOMContentLoaded", function () {
    // Grab elements, create settings, etc.
    var canvas = document.getElementById("canvas"),
        context = canvas.getContext("2d"),
        video = document.getElementById("video"),
        videoObj = { video: true };

    if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
        navigator.mediaDevices.getUserMedia(videoObj).then(function (stream) {
            video.src = window.URL.createObjectURL(stream);
            video.play();
        });
    } else {
        alert('getUserMedia() is not supported in your browser');
    }

    // Trigger photo take
    document.getElementById("snap").addEventListener("click", function () {
        context.drawImage(video, 0, 0, 640, 480);
    });

    document.getElementById("upload").addEventListener("click", function () {
        checkFace();
    });

    function checkFace() {
        $("#upload").attr('disabled', 'disabled');
        $("#upload").attr("value", "Uploading...");
        var img = canvas.toDataURL('image/jpeg', 0.9).split(',')[1];
        $.ajax({
            url: "ajax/checkFace",
            type: "POST",
            data: JSON.stringify({ image: img, test: "test" }),
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: function (data) {
                alert(JSON.stringify(data));
                $("#upload").removeAttr('disabled');
                $("#upload").attr("value", "Upload");
            },
            error: function () {
                alert("There was some error while uploading Image");
                $("#upload").removeAttr('disabled');
                $("#upload").attr("value", "Upload");
            }
        });
    }
}, false);