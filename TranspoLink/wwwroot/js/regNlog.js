/* =========================================================
   TRANSPO LINK AUTHENTICATION LOGIC
   ========================================================= */

$(document).ready(function () {
    if ($('.auth-container').length || $('.auth-form').length) {
        initAuthPages();
    }
});

function initAuthPages() {
    console.log('Auth Initialized');
    setupPasswordToggle();
    setupFormValidation();
    setupPhotoUpload();
    setupInputAnimations();
    setupDynamicInputIcon();
    setupTermsValidation();
    setupPasswordRequirements(); // <--- NEW: Real-time Check

    // Focus first input
    setTimeout(() => {
        $('.auth-input-group input:visible:first').focus();
    }, 300);
}

// ============================================================================
// REAL-TIME PASSWORD REQUIREMENT POPUP
// ============================================================================
function setupPasswordRequirements() {
    // Select the password input on Register page
    const $passInput = $('#regPassword');
    const $popup = $('#passwordPopup');

    if ($passInput.length && $popup.length) {

        // Show/Hide on focus/blur
        $passInput.on('focus', function () {
            $popup.fadeIn(200);
        });

        $passInput.on('blur', function () {
            $popup.fadeOut(200);
        });

        // Real-time check
        $passInput.on('input', function () {
            const val = $(this).val();

            // 1. One Letter
            toggleReq('#req-letter', /[a-zA-Z]/.test(val));

            // 2. Capital Letter
            toggleReq('#req-capital', /[A-Z]/.test(val));

            // 3. Number
            toggleReq('#req-number', /[0-9]/.test(val));

            // 4. Symbol
            toggleReq('#req-symbol', /[^a-zA-Z0-9]/.test(val));

            // 5. Length (8+)
            toggleReq('#req-length', val.length >= 8);
        });
    }
}

function toggleReq(id, isValid) {
    const $el = $(id);
    const $icon = $el.find('.req-icon');

    if (isValid) {
        $el.removeClass('invalid').addClass('valid');
        $icon.text('✔');
    } else {
        $el.removeClass('valid').addClass('invalid');
        $icon.text('✖');
    }
}

// ============================================================================
// CHECKBOX VALIDATION (Disables Button until Checked)
// ============================================================================
function setupTermsValidation() {
    const $check = $('#termsCheck');
    const $btn = $('#loginBtn').length ? $('#loginBtn') : $('#regBtn');

    if ($check.length && $btn.length) {
        function toggle() {
            if ($check.is(':checked')) {
                $btn.prop('disabled', false);
            } else {
                $btn.prop('disabled', true);
            }
        }
        toggle(); // On load
        $check.on('change', toggle);
    }
}

// ============================================================================
// DYNAMIC ICON SWITCHER (Email 📧 <-> Phone 📞)
// ============================================================================
function setupDynamicInputIcon() {
    $(document).on('input propertychange paste', 'input[name="Input"]', function () {
        const val = $(this).val().trim();
        const $container = $(this).closest('.auth-input-group');
        const $emailIcon = $container.find('.icon-email');
        const $phoneIcon = $container.find('.icon-phone');

        // Logic: digits, plus, dash, space -> Phone
        if (val.length > 0 && /^[0-9+\-\s]+$/.test(val)) {
            $emailIcon.hide();
            $phoneIcon.show();
        } else {
            $emailIcon.show();
            $phoneIcon.hide();
        }
    });
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

        const $svg = $btn.find('svg path');
        if (newType === 'text') {
            $svg.attr('d', 'M11.83,9L15,12.16C15,12.11 15,12.05 15,12A3,3 0 0,0 12,9C11.94,9 11.89,9 11.83,9M7.53,9.8L9.08,11.35C9.03,11.56 9,11.77 9,12A3,3 0 0,0 12,15C12.22,15 12.44,14.97 12.65,14.92L14.2,16.47C13.53,16.8 12.79,17 12,17A5,5 0 0,1 7,12C7,11.21 7.2,10.47 7.53,9.8M2,4.27L4.28,6.55L4.73,7C3.08,8.3 1.78,10 1,12C2.73,16.39 7,19.5 12,19.5C13.55,19.5 15.03,19.2 16.38,18.66L16.81,19.08L19.73,22L21,20.73L3.27,3M12,7A5,5 0 0,1 17,12C17,12.64 16.87,13.26 16.64,13.82L19.57,16.75C21.07,15.5 22.27,13.86 23,12C21.27,7.61 17,4.5 12,4.5C10.6,4.5 9.26,4.75 8,5.2L10.17,7.35C10.74,7.13 11.35,7 12,7Z');
        } else {
            $svg.attr('d', 'M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7A5,5 0 0,1 17,12A5,5 0 0,1 17,12A5,5 0 0,1 12,17M12,4.5C7,4.5 2.73,7.61 1,12C2.73,16.39 7,19.5 12,19.5C17,19.5 21.27,16.39 23,12C21.27,7.61 17,4.5 12,4.5Z');
        }
    });
}

function setupFormValidation() {
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

function setupPhotoUpload() {
    $(document).on('change', '.auth-upload input[type="file"]', function (e) {
        const file = e.target.files[0];
        const $input = $(this);
        const $img = $input.siblings('img');
        const $uploadText = $input.siblings('.auth-upload-text');

        if (!$img.data('original-src')) {
            $img.data('original-src', $img.attr('src'));
        }

        if (file) {
            if (!file.type.startsWith('image/')) {
                alert("Please select a valid image (JPG or PNG).");
                return;
            }
            const reader = new FileReader();
            reader.onload = function (e) {
                $img.attr('src', e.target.result);
                $img.css({ 'border': '3px solid #0066cc' });
                if ($uploadText.length) $uploadText.text('Photo Selected');
            };
            reader.readAsDataURL(file);
        }
    });
}

function setupInputAnimations() {
    $(document).on('focus', '.auth-input-group input', function () {
        $(this).parent().addClass('focused');
    });
    $(document).on('blur', '.auth-input-group input', function () {
        $(this).parent().removeClass('focused');
    });
}