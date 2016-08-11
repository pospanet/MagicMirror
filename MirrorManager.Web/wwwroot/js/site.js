// Write your Javascript code.
function hasGetUserMedia() {
    // Note: Opera is unprefixed.
    return !!(navigator.getUserMedia || navigator.webkitGetUserMedia ||
    navigator.mozGetUserMedia || navigator.msGetUserMedia);
}

if (hasGetUserMedia()) {
    // Good to go!
} else {
    alert('getUserMedia() is not supported in your browser');
}

window.addEventListener("DOMContentLoaded", function () {
    // Grab elements, create settings, etc.
    var canvas = document.getElementById("canvas"),
        context = canvas.getContext("2d"),
        video = document.getElementById("video"),
        videoObj = { "video": true },
        errBack = function (error) {
            console.log("Video capture error: ", error.code);
        };

    if (navigator.getUserMedia) { // Standard
        navigator.getUserMedia(videoObj, function (stream) {
            video.src = stream;
            video.play();
        }, errBack);
    } else if (navigator.webkitGetUserMedia) { // WebKit-prefixed
        navigator.webkitGetUserMedia(videoObj, function (stream) {
            video.src = window.webkitURL.createObjectURL(stream);
            video.play();
        }, errBack);
    }

    // Trigger photo take
    document.getElementById("snap").addEventListener("click", function () {
        context.drawImage(video, 0, 0, 640, 480);
    });

    document.getElementById("upload").addEventListener("click", function () {
        UploadToCloud();
    });

    function UploadToCloud() {
        $("#upload").attr('disabled', 'disabled');
        $("#upload").attr("value", "Uploading...");
        var img = canvas.toDataURL('image/jpeg', 0.9).split(',')[1];
        $.ajax({
            url: "Home/Upload",
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