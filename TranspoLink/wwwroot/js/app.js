// Initiate GET request (AJAX-supported)
$(document).on('click', '[data-get]', e => {
    e.preventDefault();
    const url = e.target.dataset.get;
    location = url || location;
});

// Initiate POST request (AJAX-supported)
$(document).on('click', '[data-post]', e => {
    e.preventDefault();
    const url = e.target.dataset.post;
    const f = $('<form>').appendTo(document.body)[0];
    f.method = 'post';
    f.action = url || location;
    f.submit();
});

// Trim input
$('[data-trim]').on('change', e => {
    e.target.value = e.target.value.trim();
});

// Auto uppercase
$('[data-upper]').on('input', e => {
    const a = e.target.selectionStart;
    const b = e.target.selectionEnd;
    e.target.value = e.target.value.toUpperCase();
    e.target.setSelectionRange(a, b);
});

// RESET form
$('[type=reset]').on('click', e => {
    e.preventDefault();
    location = location;
});

// Check all checkboxes
$('[data-check]').on('click', e => {
    e.preventDefault();
    const name = e.target.dataset.check;
    $(`[name=${name}]`).prop('checked', true);
});

// Uncheck all checkboxes
$('[data-uncheck]').on('click', e => {
    e.preventDefault();
    const name = e.target.dataset.uncheck;
    $(`[name=${name}]`).prop('checked', false);
});

// Row checkable (AJAX-supported)
$(document).on('click', '[data-checkable]', e => {
    if ($(e.target).is(':input,a')) return;

    $(e.currentTarget)
        .find(':checkbox')
        .prop('checked', (i, v) => !v);
});

// Photo preview
$('.upload input').on('change', e => {
    const f = e.target.files[0];
    const img = $(e.target).siblings('img')[0];
    img.dataset.src ??= img.src;
    if (f && f.type.startsWith('image/')) {
        img.onload = e => URL.revokeObjectURL(img.src);
        img.src = URL.createObjectURL(f);
    }
    else {
        img.src = img.dataset.src;
        e.target.value = '';
    }
    // Trigger input validation
    $(e.target).valid();
});

// ============================================================================
// MAIN FUNCTIONALITY
// ============================================================================

$(document).ready(function () {
    // ============================================================================
    // 🌙 THEME TOGGLE LOGIC (Added here!)
    // ============================================================================
    const themeToggle = $('#themeToggle');
    const themeIcon = $('#themeIcon');
    const themeText = $('#themeText');
    const htmlElement = document.documentElement; // The <html> tag

    // 1. Check saved theme on load
    const savedTheme = localStorage.getItem('theme');
    if (savedTheme === 'dark') {
        htmlElement.setAttribute('data-theme', 'dark');
        themeIcon.text('🌙');
        themeText.text('Night');
    } else {
        htmlElement.removeAttribute('data-theme');
        themeIcon.text('☀️');
        themeText.text('Light');
    }

    // 2. Handle Click
    themeToggle.on('click', function () {
        if (htmlElement.hasAttribute('data-theme')) {
            htmlElement.removeAttribute('data-theme');
            themeIcon.text('☀️');
            themeText.text('Light');
            localStorage.setItem('theme', 'light');
        } else {
            // Switch to Dark
            htmlElement.setAttribute('data-theme', 'dark');
            themeIcon.text('🌙');
            themeText.text('Night');
            localStorage.setItem('theme', 'dark');
        }
    });

    // ============================================================================
    // PROFILE DROPDOWN LOGIC
    // ============================================================================

    // Toggle dropdown when clicking the name/photo
    $('#profileTrigger').on('click', function (e) {
        e.stopPropagation(); // Prevents the click from reaching the document
        $('#profileDropdown').toggle(); // Shows or Hides
    });

    // Close dropdown when clicking ANYWHERE else on the page
    $(document).on('click', function (e) {
        if (!$(e.target).closest('.profile-dropdown').length && !$(e.target).closest('#profileTrigger').length) {
            $('#profileDropdown').hide();
        }
    });

    // ============================================================================
    // NEW: NAV BAR USER MANAGEMENT DROPDOWN LOGIC
    // ============================================================================
    $('#navUserTrigger').on('click', function (e) {
        e.stopPropagation();
        $('#navUserContent').toggleClass('show-nav-dropdown');
    });

    $(document).on('click', function (e) {
        if (!$(e.target).closest('#navUserTrigger').length) {
            $('#navUserContent').removeClass('show-nav-dropdown');
        }
    });

    // ============================================================================
    // HOME PAGE FUNCTIONALITY
    // ============================================================================

    // Tab switching functionality for transport types
    $('.tab').on('click', function () {
        $('.tab').removeClass('active');
        $(this).addClass('active');

        // Optional: You can add different behavior based on transport type
        const transportType = $(this).data('transport');
        console.log('Selected transport:', transportType);

        // Add visual feedback
        $(this).css('transform', 'scale(0.95)');
        setTimeout(() => {
            $(this).css('transform', '');
        }, 100);
    });

    // Swap origin and destination
    $('.swap-btn').on('click', function () {
        const origin = $('#origin');
        const destination = $('#destination');

        // Swap values
        const temp = origin.val();
        origin.val(destination.val());
        destination.val(temp);

        // Add rotation animation
        $(this).css('transform', 'rotate(180deg)');
        setTimeout(() => {
            $(this).css('transform', '');
        }, 300);
    });

    // Set minimum date to today for date inputs
    const today = new Date().toISOString().split('T')[0];
    $('#departDate').attr('min', today);
    $('#returnDate').attr('min', today);

    // Update return date minimum when depart date changes
    $('#departDate').on('change', function () {
        const departDate = $(this).val();
        $('#returnDate').attr('min', departDate);

        // Clear return date if it's before depart date
        const returnDate = $('#returnDate').val();
        if (returnDate && returnDate < departDate) {
            $('#returnDate').val('');
        }
    });

    // Form validation before submit
    $('.search-form').on('submit', function (e) {
        const origin = $('#origin').val().trim();
        const destination = $('#destination').val().trim();
        const departDate = $('#departDate').val();

        // Check if origin and destination are the same
        if (origin && destination && origin.toLowerCase() === destination.toLowerCase()) {
            e.preventDefault();
            alert('⚠️ Origin and destination cannot be the same!');
            return false;
        }

        // Basic validation
        if (!origin || !destination || !departDate) {
            e.preventDefault();
            alert('⚠️ Please fill in all required fields!');
            return false;
        }

        // Show loading state
        const btn = $(this).find('.search-btn');
        btn.prop('disabled', true);
        btn.html('🔍 Searching...');
    });

    // Smooth scroll for navigation links
    $('a[href^="#"]').on('click', function (e) {
        const target = $(this).attr('href');

        // Don't prevent default for # only
        if (target === '#') return;

        const targetElement = $(target);
        if (targetElement.length) {
            e.preventDefault();
            $('html, body').animate({
                scrollTop: targetElement.offset().top - 70 // Offset for sticky nav
            }, 600, 'swing');
        }
    });

    const popularCities = [
        'Kuala Lumpur',
        'Penang',
        'Johor Bahru',
        'Ipoh',
        'Melaka',
        'Kuching',
        'Kota Kinabalu',
        'Shah Alam',
        'Putrajaya',
        'Langkawi',
        'Singapore',
        'Seremban',
        'Kuantan',
        'Alor Setar',
        'Kota Bharu'
    ];

    // Simple autocomplete for origin and destination
    $('#origin, #destination').on('input', function () {
        const input = $(this).val().toLowerCase();
        if (input.length < 2) return;

        // Filter cities
        const matches = popularCities.filter(city =>
            city.toLowerCase().includes(input)
        );

        // You can implement a dropdown here if you want
        // For now, we'll just log the matches
        console.log('Matches:', matches);
    });

    // Add loading animation to trending items
    $('.trending-item').each(function (index) {
        $(this).css({
            'opacity': '0',
            'transform': 'translateY(20px)'
        });

        setTimeout(() => {
            $(this).css({
                'opacity': '1',
                'transform': 'translateY(0)',
                'transition': 'all 0.5s ease'
            });
        }, index * 100);
    });

    // Add hover effect to features
    $('.feature').on('mouseenter', function () {
        $(this).find('.feature-icon').css('transform', 'scale(1.1) rotate(5deg)');
    }).on('mouseleave', function () {
        $(this).find('.feature-icon').css('transform', 'scale(1) rotate(0deg)');
    });

    // Popular routes click handler
    $('[style*="border-left"]').on('click', function () {
        const routeText = $(this).find('div:first').text();
        const [origin, destination] = routeText.split('→').map(s => s.trim());

        if (origin && destination) {
            $('#origin').val(origin);
            $('#destination').val(destination);

            // Smooth scroll to search form
            $('html, body').animate({
                scrollTop: $('.search-container').offset().top - 100
            }, 600);

            // Highlight the form
            $('.search-container').css('box-shadow', '0 0 30px rgba(102,126,234,0.5)');
            setTimeout(() => {
                $('.search-container').css('box-shadow', '0 10px 40px rgba(0,0,0,0.15)');
            }, 2000);
        }
    });

    // Add keyboard shortcuts
    $(document).on('keydown', function (e) {
        // Alt + S to focus on search
        if (e.altKey && e.key === 's') {
            e.preventDefault();
            $('#origin').focus();
        }

        // Alt + W to swap origin/destination
        if (e.altKey && e.key === 'w') {
            e.preventDefault();
            $('.swap-btn').click();
        }
    });

    // Animate stats on scroll (Why Choose Us section)
    const animateStats = () => {
        $('.why-choose-stats').each(function () {
            const elementTop = $(this).offset().top;
            const elementBottom = elementTop + $(this).outerHeight();
            const viewportTop = $(window).scrollTop();
            const viewportBottom = viewportTop + $(window).height();

            if (elementBottom > viewportTop && elementTop < viewportBottom) {
                $(this).find('[style*="font-size: 48px"]').each(function () {
                    const target = $(this).text();
                    if (!$(this).data('animated')) {
                        $(this).data('animated', true);
                        // Animate number counting (simple version)
                        $(this).css('opacity', '1');
                    }
                });
            }
        });
    };

    // Check on scroll
    $(window).on('scroll', animateStats);
    animateStats(); // Check on load

    // Add parallax effect to hero section (subtle)
    $(window).on('scroll', function () {
        const scrolled = $(window).scrollTop();
        $('.hero').css('transform', 'translateY(' + (scrolled * 0.3) + 'px)');
    });

    // Responsive navigation toggle (for mobile - if you add a hamburger menu)
    $('.menu-toggle').on('click', function () {
        $('.nav-links').toggleClass('active');
        $(this).toggleClass('active');
    });

    // Add ripple effect to buttons
    $('.search-btn, .tab').on('click', function (e) {
        const ripple = $('<span class="ripple"></span>');
        $(this).append(ripple);

        const x = e.pageX - $(this).offset().left;
        const y = e.pageY - $(this).offset().top;

        ripple.css({
            left: x + 'px',
            top: y + 'px'
        }).addClass('ripple-effect');

        setTimeout(() => {
            ripple.remove();
        }, 600);
    });

    // ------------------------------------------------------------------------
    // FLASH MESSAGE LOGIC (UPDATED)
    // ------------------------------------------------------------------------
    const infoBox = $('.info');
    if (infoBox.text().trim().length > 0) {

        // If it's a "Welcome" message, apply the fast-fade class
        if (infoBox.text().includes('Welcome')) {
            infoBox.addClass('fast-fade');
            // Force removal after animation to be safe
            setTimeout(() => {
                infoBox.hide();
            }, 1500);
        }
        else {
            // Normal message handling (CSS animation 'fade' handles appearance)
            // Just ensure it hides eventually if animation ends
            setTimeout(() => {
                infoBox.hide();
            }, 5500);
        }
    }

});

// ============================================================================
// UTILITY FUNCTIONS
// ============================================================================

// Format date to readable format
function formatDate(dateString) {
    const options = { year: 'numeric', month: 'long', day: 'numeric' };
    return new Date(dateString).toLocaleDateString('en-MY', options);
}

// Validate Malaysian phone number
function validatePhone(phone) {
    const phoneRegex = /^(\+?6?01)[0-46-9]-*[0-9]{7,8}$/;
    return phoneRegex.test(phone);
}

// Calculate days between dates
function daysBetween(date1, date2) {
    const oneDay = 24 * 60 * 60 * 1000;
    const firstDate = new Date(date1);
    const secondDate = new Date(date2);
    return Math.round(Math.abs((firstDate - secondDate) / oneDay));
}

// Check if date is weekend
function isWeekend(date) {
    const day = new Date(date).getDay();
    return day === 0 || day === 6; // Sunday or Saturday
}