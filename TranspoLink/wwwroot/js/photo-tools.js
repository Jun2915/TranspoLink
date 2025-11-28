$(document).ready(function () {
    // Variables
    let cropper = null;
    let webcamStream = null;
    const $fileInput = $('input[type="file"][name="Photo"]'); // Target the existing file input
    const $previewImg = $('.auth-upload img'); // The preview image on Register page

    // Inject Modals into the DOM (so we don't clog up the cshtml)
    $('body').append(`
        <div id="webcamModal" class="photo-modal-overlay">
            <div class="photo-modal-content">
                <h3 style="color:white; margin-bottom:15px;">📸 Take Photo</h3>
                <div class="video-container">
                    <video id="webcamVideo" autoplay playsinline></video>
                </div>
                <div class="photo-actions">
                    <button type="button" class="btn-tool btn-capture" onclick="snapPhoto()">Capture</button>
                    <button type="button" class="btn-tool btn-cancel" onclick="closeWebcam()">Cancel</button>
                </div>
            </div>
        </div>

        <div id="cropperModal" class="photo-modal-overlay">
            <div class="photo-modal-content">
                <h3 style="color:white; margin-bottom:15px;">✂️ Edit Photo</h3>
                <div class="cropper-container-box">
                    <img id="imageToCrop" src="">
                </div>
                <div class="photo-actions">
                    <button type="button" class="btn-tool btn-rotate" onclick="rotateImage(-90)">↺</button>
                    <button type="button" class="btn-tool btn-rotate" onclick="rotateImage(90)">↻</button>
                    <button type="button" class="btn-tool btn-confirm" onclick="finishCrop()">✅ Save</button>
                    <button type="button" class="btn-tool btn-cancel" onclick="closeCropper()">Cancel</button>
                </div>
            </div>
        </div>
    `);

    // ========================================================
    // 1. WEBCAM LOGIC
    // ========================================================
    window.startWebcam = function () {
        $('#webcamModal').css('display', 'flex');
        const video = document.getElementById('webcamVideo');

        if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
            navigator.mediaDevices.getUserMedia({ video: true })
                .then(function (stream) {
                    webcamStream = stream;
                    video.srcObject = stream;
                })
                .catch(function (error) {
                    alert("Error accessing webcam: " + error.message);
                    closeWebcam();
                });
        }
    };

    window.closeWebcam = function () {
        $('#webcamModal').hide();
        if (webcamStream) {
            webcamStream.getTracks().forEach(track => track.stop());
        }
    };

    window.snapPhoto = function () {
        const video = document.getElementById('webcamVideo');
        const canvas = document.createElement('canvas');
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

        // Convert to data URL and open Cropper
        const dataUrl = canvas.toDataURL('image/jpeg');
        closeWebcam();
        openCropper(dataUrl);
    };

    // ========================================================
    // 2. CROPPER LOGIC
    // ========================================================

    // Intercept standard file selection to show Cropper
    // Note: We use a delegate or simple click handler override logic
    // But since the original input is "hidden", we hook into its change event.

    $fileInput.on('change', function (e) {
        // If this change event was triggered manually by us (the 'finishCrop' function), ignore it.
        if ($(this).data('manual-update')) {
            $(this).data('manual-update', false);
            return;
        }

        const files = e.target.files;
        if (files && files.length > 0) {
            const file = files[0];
            const reader = new FileReader();
            reader.onload = function (e) {
                openCropper(e.target.result);
                // Clear input value so we can re-select the same file if cancelled
                $fileInput.val('');
            };
            reader.readAsDataURL(file);
        }
    });

    window.openCropper = function (imageSrc) {
        $('#cropperModal').css('display', 'flex');
        const image = document.getElementById('imageToCrop');
        image.src = imageSrc;

        // Initialize Cropper.js
        if (cropper) cropper.destroy();
        cropper = new Cropper(image, {
            aspectRatio: 1, // 1:1 for profile photos (Square)
            viewMode: 1,
            autoCropArea: 1,
        });
    };

    window.closeCropper = function () {
        $('#cropperModal').hide();
        if (cropper) cropper.destroy();
        cropper = null;
    };

    window.rotateImage = function (degree) {
        if (cropper) cropper.rotate(degree);
    };

    window.finishCrop = function () {
        if (!cropper) return;

        // Get cropped canvas
        const canvas = cropper.getCroppedCanvas({
            width: 400, // Resize output to reasonable size
            height: 400
        });

        // Convert canvas to Blob (file object)
        canvas.toBlob(function (blob) {
            // Create a new File object
            const file = new File([blob], "profile_photo.jpg", { type: "image/jpeg" });

            // Create a DataTransfer to update the file input
            const dataTransfer = new DataTransfer();
            dataTransfer.items.add(file);

            // Update the file input
            $fileInput.data('manual-update', true); // Flag to prevent infinite loop
            $fileInput[0].files = dataTransfer.files;

            // Update the preview image on the page
            $previewImg.attr('src', canvas.toDataURL());
            $previewImg.css('border', '3px solid #2ed573'); // Green border indicating success
            $('.auth-upload-text').text('Photo Ready!');

            closeCropper();
        }, 'image/jpeg');
    };
});