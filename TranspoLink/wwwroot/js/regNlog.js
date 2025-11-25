// ============================================================================
// MAIN INITIALIZATION
// ============================================================================

$(document).ready(function () {
    // Only run on authentication pages
    if ($('.auth-container').length || $('.auth-form').length) {
        initAuthPages();
    }
});

function initAuthPages() {
    console.log('Authentication page initialized');

    // Setup all authentication features
    setupPasswordToggle();
    setupLoginTypeToggle();
    setupRegisterTypeToggle();
    setupFormValidation();
    setupSocialLogin();
    setupPhotoUpload();
    setupKeyboardShortcuts();
    setupInputAnimations();

    // Auto-focus first input
    setTimeout(() => {
        $('.auth-input-group input:visible:first').focus();
    }, 300);
}

// ============================================================================
// PASSWORD VISIBILITY TOGGLE
// ============================================================================

function setupPasswordToggle() {
    // Use event delegation for dynamically loaded content
    $(document).on('click', '.auth-toggle-password', function (e) {
        e.preventDefault();
        e.stopPropagation();

        const $btn = $(this);
        const $input = $btn.siblings('input');

        // Toggle input type
        const currentType = $input.attr('type');
        const newType = currentType === 'password' ? 'text' : 'password';
        $input.attr('type', newType);

        // Update icon
        const $svg = $btn.find('svg path');
        if (newType === 'text') {
            // Eye-off icon (password visible)
            $svg.attr('d', 'M11.83,9L15,12.16C15,12.11 15,12.05 15,12A3,3 0 0,0 12,9C11.94,9 11.89,9 11.83,9M7.53,9.8L9.08,11.35C9.03,11.56 9,11.77 9,12A3,3 0 0,0 12,15C12.22,15 12.44,14.97 12.65,14.92L14.2,16.47C13.53,16.8 12.79,17 12,17A5,5 0 0,1 7,12C7,11.21 7.2,10.47 7.53,9.8M2,4.27L4.28,6.55L4.73,7C3.08,8.3 1.78,10 1,12C2.73,16.39 7,19.5 12,19.5C13.55,19.5 15.03,19.2 16.38,18.66L16.81,19.08L19.73,22L21,20.73L3.27,3M12,7A5,5 0 0,1 17,12C17,12.64 16.87,13.26 16.64,13.82L19.57,16.75C21.07,15.5 22.27,13.86 23,12C21.27,7.61 17,4.5 12,4.5C10.6,4.5 9.26,4.75 8,5.2L10.17,7.35C10.74,7.13 11.35,7 12,7Z');
            $btn.attr('title', 'Hide password');
        } else {
            // Eye icon (password hidden)
            $svg.attr('d', 'M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17M12,4.5C7,4.5 2.73,7.61 1,12C2.73,16.39 7,19.5 12,19.5C17,19.5 21.27,16.39 23,12C21.27,7.61 17,4.5 12,4.5Z');
            $btn.attr('title', 'Show password');
        }

        // Add animation
        $btn.css('transform', 'scale(0.9)');
        setTimeout(() => {
            $btn.css('transform', 'scale(1)');
        }, 100);
    });
}

// ============================================================================
// LOGIN TYPE TOGGLE (Email/Phone)
// ============================================================================

function setupLoginTypeToggle() {
    $(document).on('click', '[data-login-type]', function (e) {
        e.preventDefault();

        const $btn = $(this);
        const loginType = $btn.data('login-type');

        // Update active state
        $('[data-login-type]').removeClass('active');
        $btn.addClass('active');

        // Toggle icon visibility
        $('[data-login-type]').find('.auth-option-icon').hide();
        $('[data-login-type]').find('.auth-option-icon-empty').show();
        $btn.find('.auth-option-icon').show();
        $btn.find('.auth-option-icon-empty').hide();

        // Show/hide appropriate input groups
        if (loginType === 'email') {
            $('#email-input-group').slideDown(300);
            $('#phone-input-group').slideUp(300);

            // Focus on email input after animation
            setTimeout(() => {
                $('#email-input-group input').focus();
            }, 320);
        } else {
            $('#email-input-group').slideUp(300);
            $('#phone-input-group').slideDown(300);

            // Focus on phone input after animation
            setTimeout(() => {
                $('#phone-input-group input').focus();
            }, 320);
        }

        // Add ripple effect
        addRippleEffect($btn, e);
    });
}

// ============================================================================
// REGISTER TYPE TOGGLE (Email/Phone)
// ============================================================================

function setupRegisterTypeToggle() {
    $(document).on('click', '[data-register-type]', function (e) {
        e.preventDefault();

        const $btn = $(this);
        const registerType = $btn.data('register-type');

        // Update active state
        $('[data-register-type]').removeClass('active');
        $btn.addClass('active');

        // Toggle icon visibility
        $('[data-register-type]').find('.auth-option-icon').hide();
        $('[data-register-type]').find('.auth-option-icon-empty').show();
        $btn.find('.auth-option-icon').show();
        $btn.find('.auth-option-icon-empty').hide();

        // Show/hide appropriate input groups
        if (registerType === 'email') {
            $('#reg-email-input-group').slideDown(300);
            $('#reg-phone-input-group').slideUp(300);

            setTimeout(() => {
                $('#reg-email-input-group input').focus();
            }, 320);
        } else {
            $('#reg-email-input-group').slideUp(300);
            $('#reg-phone-input-group').slideDown(300);

            setTimeout(() => {
                $('#reg-phone-input-group input').focus();
            }, 320);
        }

        // Add ripple effect
        addRippleEffect($btn, e);
    });
}

// ============================================================================
// FORM VALIDATION
// ============================================================================

function setupFormValidation() {

    // Real-time email validation
    $(document).on('blur', 'input[type="email"], input[name="Email"]', function () {
        const email = $(this).val().trim();
        const $input = $(this);
        const $errorSpan = $input.parent().siblings('.field-validation-error');

        if (email && !isValidEmail(email)) {
            if ($errorSpan.length) {
                $errorSpan.text('Please enter a valid email address').show();
            }
            $input.parent().addClass('input-error');
        } else {
            if ($errorSpan.length) {
                $errorSpan.hide();
            }
            $input.parent().removeClass('input-error');
        }
    });

    // Password strength indicator (only on register page)
    $(document).on('input', 'input[name="Password"]', function () {
        // Check if we're on register page (has Confirm field)
        if ($('input[name="Confirm"]').length > 0) {
            const password = $(this).val();
            updatePasswordStrength(password, this);
        }
    });

    // Confirm password validation
    $(document).on('input blur', 'input[name="Confirm"]', function () {
        const password = $('input[name="Password"]').val();
        const confirm = $(this).val();
        const $errorSpan = $(this).parent().siblings('.field-validation-error');

        if (confirm && password !== confirm) {
            if ($errorSpan.length) {
                $errorSpan.text('Passwords do not match').show();
            }
            $(this).parent().addClass('input-error');
        } else {
            if ($errorSpan.length) {
                $errorSpan.hide();
            }
            $(this).parent().removeClass('input-error');
        }
    });

    // Phone number validation and formatting
    $(document).on('input', 'input[name="Phone"], input[name="PhoneNumber"]', function () {
        let value = $(this).val().replace(/\D/g, '');

        // Format Malaysian phone number
        if (value.length > 0) {
            if (value.startsWith('60')) {
                // Already has country code
                value = '+' + value;
            } else if (value.startsWith('0')) {
                // Local format, add country code
                value = '+60' + value.substring(1);
            } else if (value.startsWith('1')) {
                // Mobile without leading 0
                value = '+601' + value.substring(1);
            }
        }

        $(this).val(value);

        // Validate
        const $errorSpan = $(this).parent().siblings('.field-validation-error');
        if (value.length > 3 && !isValidMalaysianPhone(value)) {
            if ($errorSpan.length) {
                $errorSpan.text('Please enter a valid Malaysian phone number').show();
            }
            $(this).parent().addClass('input-error');
        } else {
            if ($errorSpan.length) {
                $errorSpan.hide();
            }
            $(this).parent().removeClass('input-error');
        }
    });

    // Auto-trim all inputs on blur
    $(document).on('blur', '.auth-input-group input', function () {
        $(this).val($(this).val().trim());
    });

    // Form submit validation and loading state
    $(document).on('submit', '.auth-form', function (e) {
        const $form = $(this);
        let isValid = true;

        // Validate all visible required inputs
        $form.find('input:visible[required]').each(function () {
            const value = $(this).val().trim();
            if (!value) {
                isValid = false;
                $(this).parent().addClass('input-error');

                // Show error message
                const fieldName = $(this).attr('placeholder') || $(this).attr('name');
                const $errorSpan = $(this).parent().siblings('.field-validation-error');
                if ($errorSpan.length) {
                    $errorSpan.text(fieldName + ' is required').show();
                }
            }
        });

        // Check password match on register page
        if ($('input[name="Confirm"]').is(':visible')) {
            const password = $('input[name="Password"]').val();
            const confirm = $('input[name="Confirm"]').val();
            if (password !== confirm) {
                isValid = false;
                showAuthError('Passwords do not match');
            }
        }

        if (!isValid) {
            e.preventDefault();
            showAuthError('Please fill in all required fields correctly');
            return false;
        }

        // Show loading state
        const $submitBtn = $form.find('.auth-submit-btn');
        const originalText = $submitBtn.text();
        $submitBtn.data('original-text', originalText);
        $submitBtn.prop('disabled', true);
        $submitBtn.html('<span class="auth-spinner"></span> Processing...');

        // Re-enable if there's a validation error after server response
        setTimeout(() => {
            if ($('.validation-summary-errors:visible').length > 0) {
                $submitBtn.prop('disabled', false);
                $submitBtn.text(originalText);
            }
        }, 1000);
    });
}

// ============================================================================
// SOCIAL LOGIN
// ============================================================================

function setupSocialLogin() {
    $(document).on('click', '.auth-social-btn', function (e) {
        e.preventDefault();

        const $btn = $(this);
        const provider = $btn.hasClass('google') ? 'Google' : 'Facebook';
        const originalHtml = $btn.html();

        // Add loading state
        $btn.prop('disabled', true);
        $btn.html('<span class="auth-spinner"></span> Connecting...');

        console.log(`${provider} login initiated`);

        // TODO: Implement actual OAuth flow here
        // Example: window.location.href = '/Account/ExternalLogin?provider=' + provider;

        // For demonstration - remove this and implement actual OAuth
        setTimeout(() => {
            alert(`${provider} login would be initiated here.\n\nTo implement:\n1. Add OAuth configuration in appsettings.json\n2. Configure external login in Startup.cs\n3. Create ExternalLogin action in AccountController`);
            $btn.prop('disabled', false);
            $btn.html(originalHtml);
        }, 1500);
    });
}

// ============================================================================
// PHOTO UPLOAD
// ============================================================================

function setupPhotoUpload() {
    // Photo input change handler
    $(document).on('change', '.auth-upload input[type="file"]', function (e) {
        const file = e.target.files[0];
        const $input = $(this);
        const $img = $input.siblings('img');
        const $uploadText = $input.siblings('.auth-upload-text');

        // Store original image if not already stored
        if (!$img.data('original-src')) {
            $img.data('original-src', $img.attr('src'));
        }

        if (file) {
            // Validate file type
            if (!file.type.startsWith('image/')) {
                showAuthError('Please select a valid image file (JPEG or PNG)');
                $input.val('');
                return;
            }

            // Validate file size (max 5MB)
            if (file.size > 5 * 1024 * 1024) {
                showAuthError('Photo size must be less than 5MB');
                $input.val('');
                return;
            }

            // Show preview
            const reader = new FileReader();
            reader.onload = function (e) {
                $img.attr('src', e.target.result);
                $img.css({
                    'width': '120px',
                    'height': '120px',
                    'object-fit': 'cover',
                    'border-radius': '50%',
                    'border': '3px solid #667eea'
                });

                if ($uploadText.length) {
                    $uploadText.text('Change photo');
                }
            };
            reader.readAsDataURL(file);

            // Clear any validation errors
            $input.siblings('.field-validation-error').hide();

        } else {
            // No file selected, restore original
            $img.attr('src', $img.data('original-src'));
            $img.css({
                'width': '',
                'height': '',
                'border-radius': '',
                'border': ''
            });
            if ($uploadText.length) {
                $uploadText.text('Click to upload photo');
            }
        }
    });

    // Make image clickable
    $(document).on('click', '.auth-upload img', function () {
        $(this).siblings('input[type="file"]').click();
    });
}

// ============================================================================
// INPUT ANIMATIONS
// ============================================================================

function setupInputAnimations() {
    // Add focus/blur animations
    $(document).on('focus', '.auth-input-group input', function () {
        $(this).parent().addClass('focused');
    });

    $(document).on('blur', '.auth-input-group input', function () {
        $(this).parent().removeClass('focused');
    });

    // Add filled class when input has value
    $(document).on('input', '.auth-input-group input', function () {
        if ($(this).val().length > 0) {
            $(this).parent().addClass('filled');
        } else {
            $(this).parent().removeClass('filled');
        }
    });

    // Check on page load
    $('.auth-input-group input').each(function () {
        if ($(this).val().length > 0) {
            $(this).parent().addClass('filled');
        }
    });
}

// ============================================================================
// KEYBOARD SHORTCUTS
// ============================================================================

function setupKeyboardShortcuts() {
    // Enter key navigation
    $(document).on('keypress', '.auth-input-group input', function (e) {
        if (e.which === 13) { // Enter key
            e.preventDefault();

            const $form = $(this).closest('form');
            const $visibleInputs = $form.find('.auth-input-group input:visible:not([type="hidden"])');
            const currentIndex = $visibleInputs.index(this);

            if (currentIndex < $visibleInputs.length - 1) {
                // Move to next input
                $visibleInputs.eq(currentIndex + 1).focus();
            } else {
                // Last input, submit form
                $form.submit();
            }
        }
    });

    // ESC key to clear input
    $(document).on('keydown', '.auth-input-group input', function (e) {
        if (e.which === 27) { // ESC key
            $(this).val('').trigger('input');
            $(this).blur();
        }
    });

    // Global keyboard shortcuts
    $(document).on('keydown', function (e) {
        // Only on auth pages
        if (!$('.auth-container').length) return;

        // Ctrl/Cmd + K to focus first input
        if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
            e.preventDefault();
            $('.auth-input-group input:visible:first').focus();
        }

        // Ctrl/Cmd + Enter to submit form
        if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
            e.preventDefault();
            $('.auth-form').submit();
        }
    });
}

// ============================================================================
// UTILITY FUNCTIONS
// ============================================================================

// Email validation
function isValidEmail(email) {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
}

// Malaysian phone validation
function isValidMalaysianPhone(phone) {
    // Remove all non-digits except leading +
    const cleaned = phone.replace(/[^\d+]/g, '');

    // Valid formats:
    // +60xxxxxxxxx (10-11 digits after +60)
    // 01xxxxxxxxx (10-11 digits starting with 0)
    const regex1 = /^\+60[1-9]\d{7,9}$/; // International format
    const regex2 = /^0[1-9]\d{7,9}$/;     // Local format

    return regex1.test(cleaned) || regex2.test(cleaned);
}

// Password strength calculator
function updatePasswordStrength(password, element) {
    // Remove existing strength indicator
    $(element).parent().siblings('.password-strength').remove();

    if (password.length === 0) return;

    let strength = 0;
    const checks = {
        length: password.length >= 8,
        lengthBonus: password.length >= 12,
        uppercase: /[A-Z]/.test(password),
        lowercase: /[a-z]/.test(password),
        numbers: /[0-9]/.test(password),
        special: /[^a-zA-Z0-9]/.test(password)
    };

    // Calculate strength
    if (checks.length) strength++;
    if (checks.lengthBonus) strength++;
    if (checks.uppercase && checks.lowercase) strength++;
    if (checks.numbers) strength++;
    if (checks.special) strength++;

    // Determine strength level
    let strengthClass = '';
    let strengthText = '';
    let strengthPercent = 0;

    if (strength <= 2) {
        strengthClass = 'weak';
        strengthText = 'Weak';
        strengthPercent = 33;
    } else if (strength === 3) {
        strengthClass = 'medium';
        strengthText = 'Medium';
        strengthPercent = 66;
    } else if (strength === 4) {
        strengthClass = 'strong';
        strengthText = 'Strong';
        strengthPercent = 85;
    } else {
        strengthClass = 'very-strong';
        strengthText = 'Very Strong';
        strengthPercent = 100;
    }

    // Create strength indicator
    const $strengthIndicator = $(`
        <div class="password-strength ${strengthClass}">
            <div class="password-strength-bars">
                <div class="password-strength-bar" style="width: ${strengthPercent}%"></div>
            </div>
            <span class="password-strength-text">${strengthText}</span>
        </div>
    `);

    $(element).parent().after($strengthIndicator);

    // Animate the bar
    setTimeout(() => {
        $strengthIndicator.find('.password-strength-bar').css('transition', 'width 0.3s ease');
    }, 10);
}

// Show error toast
function showAuthError(message) {
    // Remove existing toasts
    $('.auth-error-toast').remove();

    const $toast = $(`
        <div class="auth-error-toast">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="white">
                <path d="M13,13H11V7H13M13,17H11V15H13M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z" />
            </svg>
            <span>${message}</span>
        </div>
    `);

    $('body').append($toast);

    // Trigger animation
    setTimeout(() => {
        $toast.addClass('show');
    }, 100);

    // Auto remove after 4 seconds
    setTimeout(() => {
        $toast.removeClass('show');
        setTimeout(() => {
            $toast.remove();
        }, 300);
    }, 4000);
}

// Add ripple effect
function addRippleEffect($element, event) {
    const $ripple = $('<span class="ripple"></span>');
    $element.css('position', 'relative').css('overflow', 'hidden');
    $element.append($ripple);

    const rect = $element[0].getBoundingClientRect();
    const x = event.pageX - rect.left - $(window).scrollLeft();
    const y = event.pageY - rect.top - $(window).scrollTop();

    $ripple.css({
        left: x + 'px',
        top: y + 'px',
        position: 'absolute',
        width: '10px',
        height: '10px',
        'border-radius': '50%',
        background: 'rgba(255, 255, 255, 0.5)',
        transform: 'scale(0)',
        animation: 'ripple-animation 0.6s ease-out',
        'pointer-events': 'none'
    });

    setTimeout(() => {
        $ripple.remove();
    }, 600);
}

// ============================================================================
// TAB SWITCHING ANIMATION
// ============================================================================

$(document).on('click', '.auth-tab', function () {
    const $form = $('.auth-form');
    $form.css('opacity', '0');
    setTimeout(() => {
        $form.css({
            'opacity': '1',
            'transition': 'opacity 0.3s ease'
        });
    }, 50);
});

// ============================================================================
// PREVENT MULTIPLE FORM SUBMISSIONS
// ============================================================================

let isSubmitting = false;
$(document).on('submit', '.auth-form', function (e) {
    if (isSubmitting) {
        e.preventDefault();
        return false;
    }

    // Mark as submitting
    isSubmitting = true;

    // Reset after 5 seconds (in case of error or redirect delay)
    setTimeout(() => {
        isSubmitting = false;
    }, 5000);
});

// ============================================================================
// REMEMBER ME TOOLTIP
// ============================================================================

$(document).on('mouseenter', '.auth-checkbox', function () {
    if (!$(this).data('tooltip-added')) {
        $(this).attr('title', 'Keep me logged in on this device');
        $(this).data('tooltip-added', true);
    }
});

// ============================================================================
// CONSOLE WELCOME MESSAGE
// ============================================================================

console.log('%c TranspoLink Authentication ', 'background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; font-size: 16px; padding: 10px 20px; border-radius: 5px;');
console.log('%c Keyboard Shortcuts: ', 'font-weight: bold; font-size: 14px; color: #667eea;');
console.log('  • Ctrl/Cmd + K: Focus first input');
console.log('  • Ctrl/Cmd + Enter: Submit form');
console.log('  • Enter: Next field or submit');
console.log('  • ESC: Clear current input');