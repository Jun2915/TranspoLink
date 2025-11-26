// ============================================================================
// MAIN INITIALIZATION
// ============================================================================

$(document).ready(function () {
    if ($('.auth-container').length || $('.auth-form').length) {
        initAuthPages();
    }
});

function initAuthPages() {
    console.log('Authentication page initialized');

    setupPasswordToggle();
    setupLoginTypeToggle();
    setupRegisterTypeToggle();
    setupFormValidation();
    setupPhotoUpload();
    setupInputAnimations();

    // Auto-focus first visible input
    setTimeout(() => {
        $('.auth-input-group input:visible:first').focus();
    }, 300);

    // PERSISTENCE: If the server returned the page with data (error state),
    // switch to the correct tab automatically so the user doesn't lose focus.
    if ($('input[name="PhoneNumber"]').val()) {
        $('[data-register-type="phone"]').click();
    }
    if ($('input[name="Phone"]').val()) {
        $('[data-login-type="phone"]').click();
    }
}

// ============================================================================
// PASSWORD VISIBILITY TOGGLE
// ============================================================================
function setupPasswordToggle() {
    $(document).on('click', '.auth-toggle-password', function (e) {
        e.preventDefault();
        e.stopPropagation();
        const $btn = $(this);
        const $input = $btn.siblings('input');
        const currentType = $input.attr('type');
        const newType = currentType === 'password' ? 'text' : 'password';
        $input.attr('type', newType);

        // Toggle Eye Icon
        const $svg = $btn.find('svg path');
        if (newType === 'text') {
            // Eye Off Icon
            $svg.attr('d', 'M11.83,9L15,12.16C15,12.11 15,12.05 15,12A3,3 0 0,0 12,9C11.94,9 11.89,9 11.83,9M7.53,9.8L9.08,11.35C9.03,11.56 9,11.77 9,12A3,3 0 0,0 12,15C12.22,15 12.44,14.97 12.65,14.92L14.2,16.47C13.53,16.8 12.79,17 12,17A5,5 0 0,1 7,12C7,11.21 7.2,10.47 7.53,9.8M2,4.27L4.28,6.55L4.73,7C3.08,8.3 1.78,10 1,12C2.73,16.39 7,19.5 12,19.5C13.55,19.5 15.03,19.2 16.38,18.66L16.81,19.08L19.73,22L21,20.73L3.27,3M12,7A5,5 0 0,1 17,12C17,12.64 16.87,13.26 16.64,13.82L19.57,16.75C21.07,15.5 22.27,13.86 23,12C21.27,7.61 17,4.5 12,4.5C10.6,4.5 9.26,4.75 8,5.2L10.17,7.35C10.74,7.13 11.35,7 12,7Z');
        } else {
            // Eye On Icon
            $svg.attr('d', 'M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7A5,5 0 0,1 17,12A5,5 0 0,1 17,12A5,5 0 0,1 12,17M12,4.5C7,4.5 2.73,7.61 1,12C2.73,16.39 7,19.5 12,19.5C17,19.5 21.27,16.39 23,12C21.27,7.61 17,4.5 12,4.5Z');
        }
    });
}

// ============================================================================
// LOGIN TYPE TOGGLE
// ============================================================================
function setupLoginTypeToggle() {
    $(document).on('click', '[data-login-type]', function (e) {
        e.preventDefault();
        const $btn = $(this);
        const loginType = $btn.data('login-type');

        // UI Toggle
        $('[data-login-type]').removeClass('active');
        $btn.addClass('active');

        // Icon Toggle
        $('[data-login-type]').find('.auth-option-icon').hide();
        $('[data-login-type]').find('.auth-option-icon-empty').show();

        $btn.find('.auth-option-icon').show();
        $btn.find('.auth-option-icon-empty').hide();

        // Input Toggle
        if (loginType === 'email') {
            $('#email-input-group').show();
            $('#phone-input-group').hide();
            // We don't clear values anymore so user data persists if they switch back and forth
        } else {
            $('#email-input-group').hide();
            $('#phone-input-group').show();
        }

        setTimeout(() => { $('.auth-input-group:visible input').first().focus(); }, 50);
    });
}

// ============================================================================
// REGISTER TYPE TOGGLE
// ============================================================================
function setupRegisterTypeToggle() {
    $(document).on('click', '[data-register-type]', function (e) {
        e.preventDefault();
        const $btn = $(this);
        const registerType = $btn.data('register-type');

        $('[data-register-type]').removeClass('active');
        $btn.addClass('active');

        $('[data-register-type]').find('.auth-option-icon').hide();
        $('[data-register-type]').find('.auth-option-icon-empty').show();

        $btn.find('.auth-option-icon').show();
        $btn.find('.auth-option-icon-empty').hide();

        if (registerType === 'email') {
            $('#reg-email-input-group').show();
            $('#reg-phone-input-group').hide();
        } else {
            $('#reg-email-input-group').hide();
            $('#reg-phone-input-group').show();
        }

        setTimeout(() => { $('.auth-input-group:visible input').first().focus(); }, 50);
    });
}

function setupFormValidation() {
    // Very flexible phone validation. Just checking if it contains valid characters.
    // No strict Malaysian regex here.
    $(document).on('input', 'input[name="Phone"], input[name="PhoneNumber"]', function () {
        let value = $(this).val();
        // Allow only numbers, spaces, dashes, and plus sign
        if (value.length > 0 && !/^[0-9+\-\s]*$/.test(value)) {
            $(this).addClass('input-error'); // Highlight red
        } else {
            $(this).removeClass('input-error'); // Remove red
        }
    });

    // Check password match
    $(document).on('input', 'input[name="Confirm"]', function () {
        const password = $('input[name="Password"]').val();
        const confirm = $(this).val();

        if (confirm && password !== confirm) {
            $(this).parent().addClass('input-error');
        } else {
            $(this).parent().removeClass('input-error');
        }
    });
}

// ============================================================================
// PHOTO UPLOAD (FIXED: Removed the "click" event that caused double opening)
// ============================================================================
function setupPhotoUpload() {
    // We only listen for the CHANGE event now.
    // The <label> HTML tag handles the click automatically.
    $(document).on('change', '.auth-upload input[type="file"]', function (e) {
        const file = e.target.files[0];
        const $input = $(this);
        const $img = $input.siblings('img');
        const $uploadText = $input.siblings('.auth-upload-text');

        // Save original src if not saved
        if (!$img.data('original-src')) {
            $img.data('original-src', $img.attr('src'));
        }

        if (file) {
            // Basic check to ensure it's an image
            if (!file.type.startsWith('image/')) {
                alert("Please select a valid image (JPG or PNG).");
                return;
            }

            const reader = new FileReader();
            reader.onload = function (e) {
                $img.attr('src', e.target.result);
                $img.css({
                    'border': '3px solid #0066cc' // Show blue border when selected
                });
                if ($uploadText.length) $uploadText.text('Photo Selected');
            };
            reader.readAsDataURL(file);
        }
    });
}

// ============================================================================
// INPUT ANIMATIONS
// ============================================================================
function setupInputAnimations() {
    $(document).on('focus', '.auth-input-group input', function () {
        $(this).parent().addClass('focused');
    });
    $(document).on('blur', '.auth-input-group input', function () {
        $(this).parent().removeClass('focused');
    });
}

// ============================================================================
// SOCIAL LOGIN (Dummy)
// ============================================================================
function setupSocialLogin() {
    $(document).on('click', '.auth-social-btn', function (e) {
        e.preventDefault();
        // Just a placeholder
        const provider = $(this).hasClass('facebook') ? "Facebook" : "Google";
        console.log("Social login clicked: " + provider);
    });
}