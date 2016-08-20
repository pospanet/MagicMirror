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

    $("#snap").on("click", function () {
        context.drawImage(video, 0, 0, 640, 480);
    });

    $("#checkFace").on("click", function () {
        checkFace();
    });
    $("#linkFace").on("click", function () {
        linkFace();
    });
    $("#deleteIdentity").on("click", function () {
        deleteIdentity();
    });

    function checkFace() {
        $("#checkFace").attr('disabled', 'disabled');
        $("#checkFace").attr("value", "Uploading...");
        var img = canvas.toDataURL('image/jpeg', 0.9).split(',')[1];
        $.ajax({
            url: "ajax/checkFace",
            type: "POST",
            data: JSON.stringify({ image: img, test: "test" }),
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
            data: JSON.stringify({ image: img, test: "test" }),
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
            data: JSON.stringify({ image: img, test: "test" }),
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