$(document).ready(function () {
    // Variables
    let cropper = null;
    let webcamStream = null;
    const $fileInput = $('input[type="file"][name="Photo"]');
    const $previewImg = $('#profileImagePreview');
    const $photoContainer = $('#photoContainer');

    // --------------------------------------------------------
    // 0. INJECT MODALS (Selection, Webcam, Cropper)
    // --------------------------------------------------------
    $('body').append(`
        <div id="sourceModal" class="photo-modal-overlay">
            <div class="photo-modal-content" style="max-width: 400px;">
                <h3 style="color:white; margin-bottom:20px;">📸 Change Profile Photo</h3>
                <p style="color:#ccc; margin-bottom:20px;">Choose how you want to upload your photo.</p>
                <div class="photo-actions" style="flex-direction: column; gap: 15px;">
                    <button type="button" class="btn-tool btn-primary-action" onclick="triggerFileInput()">📂 Upload from Files</button>
                    <button type="button" class="btn-tool btn-primary-action" onclick="startWebcam()">📸 Take a Photo</button>
                    <button type="button" class="btn-tool btn-cancel" onclick="closeSourceModal()">Cancel</button>
                </div>
            </div>
        </div>

        <div id="webcamModal" class="photo-modal-overlay">
            <div class="photo-modal-content">
                <h3 style="color:white; margin-bottom:15px;">📸 Smile!</h3>
                <div class="video-container">
                    <video id="webcamVideo" autoplay playsinline></video>
                </div>
                <div class="photo-actions">
                    <button type="button" class="btn-tool btn-capture" onclick="snapPhoto()">⚪ Capture</button>
                    <button type="button" class="btn-tool btn-cancel" onclick="closeWebcam()">Cancel</button>
                </div>
            </div>
        </div>

        <div id="cropperModal" class="photo-modal-overlay">
            <div class="photo-modal-content">
                <h3 style="color:white; margin-bottom:15px;">✂️ Edit & Rotate</h3>
                <div class="cropper-container-box">
                    <img id="imageToCrop" src="">
                </div>
                <div class="photo-actions">
                    <button type="button" class="btn-tool btn-rotate" onclick="rotateImage(-90)">↺ Left</button>
                    <button type="button" class="btn-tool btn-rotate" onclick="rotateImage(90)">↻ Right</button>
                    <button type="button" class="btn-tool btn-confirm" onclick="finishCrop()">✅ Save</button>
                    <button type="button" class="btn-tool btn-cancel" onclick="closeCropper()">Cancel</button>
                </div>
            </div>
        </div>
    `);

    // --------------------------------------------------------
    // 1. CLICK HANDLER (Edit Mode Check)
    // --------------------------------------------------------
    $photoContainer.on('click', function () {
        // Only open if the input is enabled (Edit mode active)
        if (!$fileInput.prop('disabled')) {
            $('#sourceModal').css('display', 'flex');
        }
    });

    window.closeSourceModal = function () {
        $('#sourceModal').hide();
    };

    window.triggerFileInput = function () {
        closeSourceModal();
        $fileInput.click(); // Trigger the hidden file input
    };

    // --------------------------------------------------------
    // 2. WEBCAM LOGIC
    // --------------------------------------------------------
    window.startWebcam = function () {
        closeSourceModal(); // Close selection modal first
        $('#webcamModal').css('display', 'flex');
        const video = document.getElementById('webcamVideo');

        if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
            navigator.mediaDevices.getUserMedia({ video: true })
                .then(function (stream) {
                    webcamStream = stream;
                    video.srcObject = stream;
                })
                .catch(function (error) {
                    alert("Unable to access camera: " + error.message);
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

        // Flip horizontally for mirror effect (optional, feels more natural)
        ctx.translate(canvas.width, 0);
        ctx.scale(-1, 1);

        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

        // Convert to data URL and open Cropper
        const dataUrl = canvas.toDataURL('image/jpeg');
        closeWebcam();
        openCropper(dataUrl);
    };

    // --------------------------------------------------------
    // 3. CROPPER LOGIC
    // --------------------------------------------------------

    // Listen for file selection
    $fileInput.on('change', function (e) {
        // Prevent infinite loop if we updated it manually
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
                // Clear input value so we can re-select if needed
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
            aspectRatio: 1, // Square for profile
            viewMode: 1,    // Restrict crop box to canvas
            autoCropArea: 1,
            background: false, // Cleaner look
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

        // Get cropped canvas WITH styling to ensure rotation works
        const canvas = cropper.getCroppedCanvas({
            width: 400,
            height: 400,
            fillColor: '#fff', // Important for JPEGs to avoid black background on rotation
            imageSmoothingEnabled: true,
            imageSmoothingQuality: 'high',
        });

        if (!canvas) {
            alert("Could not crop image. Please try again.");
            return;
        }

        // Convert canvas to Blob
        canvas.toBlob(function (blob) {
            // Create a new File object
            const file = new File([blob], "profile_photo.jpg", { type: "image/jpeg" });

            // Update the file input
            const dataTransfer = new DataTransfer();
            dataTransfer.items.add(file);

            $fileInput.data('manual-update', true);
            $fileInput[0].files = dataTransfer.files;

            // Update Preview
            $previewImg.attr('src', canvas.toDataURL());
            $previewImg.css('border', '3px solid #2ed573'); // Success border

            closeCropper();
        }, 'image/jpeg', 0.9); // 0.9 quality
    };
});