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

    // PERSISTENCE: Check which fields have values on load (in case of error)
    // If Phone has a value, switch to the phone tab automatically
    if ($('input[name="PhoneNumber"]').val() || $('input[name="Phone"]').val()) {
        $('[data-register-type="phone"]').click();
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
            $svg.attr('d', 'M11.83,9L15,12.16C15,12.11 15,12.05 15,12A3,3 0 0,0 12,9C11.94,9 11.89,9 11.83,9M7.53,9.8L9.08,11.35C9.03,11.56 9,11.77 9,12A3,3 0 0,0 12,15C12.22,15 12.44,14.97 12.65,14.92L14.2,16.47C13.53,16.8 12.79,17 12,17A5,5 0 0,1 7,12C7,11.21 7.2,10.47 7.53,9.8M2,4.27L4.28,6.55L4.73,7C3.08,8.3 1.78,10 1,12C2.73,16.39 7,19.5 12,19.5C13.55,19.5 15.03,19.2 16.38,18.66L16.81,19.08L19.73,22L21,20.73L3.27,3M12,7A5,5 0 0,1 17,12C17,12.64 16.87,13.26 16.64,13.82L19.57,16.75C21.07,15.5 22.27,13.86 23,12C21.27,7.61 17,4.5 12,4.5C10.6,4.5 9.26,4.75 8,5.2L10.17,7.35C10.74,7.13 11.35,7 12,7Z');
        } else {
            $svg.attr('d', 'M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17M12,4.5C7,4.5 2.73,7.61 1,12C2.73,16.39 7,19.5 12,19.5C17,19.5 21.27,16.39 23,12C21.27,7.61 17,4.5 12,4.5Z');
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
            // Clear phone value so validation doesn't complain about it
            // $('input[name="Phone"]').val(''); 
        } else {
            $('#email-input-group').hide();
            $('#phone-input-group').show();
            // Clear email value
            // $('input[name="Email"]').val('');
        }

        // Focus input
        setTimeout(() => {
            $('.auth-input-group:visible input').first().focus();
        }, 50);
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

        setTimeout(() => {
            $('.auth-input-group:visible input').first().focus();
        }, 50);
    });
}

// ============================================================================
// FORM VALIDATION
// ============================================================================
function setupFormValidation() {
    // Relaxed Phone Validation
    $(document).on('input', 'input[name="Phone"], input[name="PhoneNumber"]', function () {
        let value = $(this).val();
        // Just allow numbers and plus sign, don't force aggressive formatting
        // This makes it less annoying ("gg") for users
        const $errorSpan = $(this).parent().siblings('.field-validation-error');

        if (value.length > 0 && !/^[0-9+]*$/.test(value)) {
            // Only error if they type letters
            $(this).addClass('input-error');
        } else {
            $(this).removeClass('input-error');
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
// PHOTO UPLOAD (FIXED: Removed double click handler)
// ============================================================================
function setupPhotoUpload() {
    // Only handle the change event. 
    // The Label > Input structure in HTML handles the click automatically.
    $(document).on('change', '.auth-upload input[type="file"]', function (e) {
        const file = e.target.files[0];
        const $input = $(this);
        const $img = $input.siblings('img');
        const $uploadText = $input.siblings('.auth-upload-text');

        if (!$img.data('original-src')) {
            $img.data('original-src', $img.attr('src'));
        }

        if (file) {
            const reader = new FileReader();
            reader.onload = function (e) {
                $img.attr('src', e.target.result);
                // Apply styling to indicate selected
                $img.css({
                    'border': '3px solid #0066cc'
                });
                if ($uploadText.length) $uploadText.text('Change photo');
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
        const provider = $(this).text().trim();
        alert(provider + ' login coming soon!');
    });
}

// ============================================================================
// HELPER FOR RIPPLE EFFECT
// ============================================================================
function addRippleEffect($element, event) {
    const $ripple = $('<span class="ripple"></span>');
    $element.css('position', 'relative').css('overflow', 'hidden');
    $element.append($ripple);
    const rect = $element[0].getBoundingClientRect();
    const x = event.pageX - rect.left - $(window).scrollLeft();
    const y = event.pageY - rect.top - $(window).scrollTop();
    $ripple.css({
        left: x + 'px', top: y + 'px',
        position: 'absolute', width: '10px', height: '10px',
        'border-radius': '50%', background: 'rgba(255, 255, 255, 0.5)',
        transform: 'scale(0)', animation: 'ripple-animation 0.6s ease-out',
        'pointer-events': 'none'
    });
    setTimeout(() => { $ripple.remove(); }, 600);
}