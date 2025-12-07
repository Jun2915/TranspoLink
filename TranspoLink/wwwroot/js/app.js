// ============================================================================
// 1. GLOBAL HELPERS (AJAX, INPUTS, CHECKBOXES, FILE PREVIEW)
// ============================================================================

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

// Check/Uncheck helpers
$('[data-check]').on('click', e => {
    e.preventDefault();
    const name = e.target.dataset.check;
    $(`[name=${name}]`).prop('checked', true);
});

$('[data-uncheck]').on('click', e => {
    e.preventDefault();
    const name = e.target.dataset.uncheck;
    $(`[name=${name}]`).prop('checked', false);
});

// Photo preview
$('.upload input').on('change', e => {
    const f = e.target.files[0];
    const img = $(e.target).siblings('img')[0];
    img.dataset.src ??= img.src;
    if (f && f.type.startsWith('image/')) {
        img.onload = e => URL.revokeObjectURL(img.src);
        img.src = URL.createObjectURL(f);
    } else {
        img.src = img.dataset.src;
        e.target.value = '';
    }
    $(e.target).valid(); // Trigger validation
});

// ============================================================================
// 2. MAIN FUNCTIONALITY (DOM READY)
// ============================================================================

$(document).ready(function () {

    // ------------------------------------------------------------------------
    // A. THEME TOGGLE LOGIC
    // ------------------------------------------------------------------------
    const themeToggle = $('#themeToggle');
    const themeIcon = $('#themeIcon');
    const themeText = $('#themeText');
    const htmlElement = document.documentElement;

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

    themeToggle.on('click', function () {
        if (htmlElement.hasAttribute('data-theme')) {
            htmlElement.removeAttribute('data-theme');
            themeIcon.text('☀️');
            themeText.text('Light');
            localStorage.setItem('theme', 'light');
        } else {
            htmlElement.setAttribute('data-theme', 'dark');
            themeIcon.text('🌙');
            themeText.text('Night');
            localStorage.setItem('theme', 'dark');
        }
    });

    // ------------------------------------------------------------------------
    // B. DROPDOWN MENUS (Profile & Nav)
    // ------------------------------------------------------------------------
    $('#profileTrigger').on('click', function (e) {
        e.stopPropagation();
        $('#profileDropdown').toggle();
    });

    $('#navUserTrigger').on('click', function (e) {
        e.stopPropagation();
        $('#navUserContent').toggleClass('show-nav-dropdown');
    });

    $(document).on('click', function (e) {
        if (!$(e.target).closest('.profile-dropdown').length && !$(e.target).closest('#profileTrigger').length) {
            $('#profileDropdown').hide();
        }
        if (!$(e.target).closest('#navUserTrigger').length) {
            $('#navUserContent').removeClass('show-nav-dropdown');
        }
    });

    // ------------------------------------------------------------------------
    // C. HOME PAGE SEARCH LOGIC (TRANSPORT, AUTOCOMPLETE, SWAP)
    // ------------------------------------------------------------------------

    // 1. Fetch Locations for Autocomplete
    if ($('#locationList').length) {
        $.ajax({
            url: '/RouteNTrip/GetLocations',
            type: 'GET',
            success: function (data) {
                const dataList = $('#locationList');
                dataList.empty();
                data.forEach(city => {
                    dataList.append(`<option value="${city}">`);
                });
            },
            error: function (err) {
                console.error("Failed to load locations", err);
            }
        });
    }

    // 2. Transport Tab Switching
    const heroImages = {
        'Bus': '/images/bustrip_background.png',
        'Train': '/images/traintrip_background.png',
        'Ferry': '/images/ferrytrip_background.png'
    };

    // Preload images
    for (const key in heroImages) {
        new Image().src = heroImages[key];
    }

    $('.transport-tab').on('click', function () {
        // UI State
        $('.transport-tab').removeClass('active');
        $(this).addClass('active');

        // Logic
        const type = $(this).data('transport');

        // Update Hidden Input for Form Submission
        $('#transportTypeInput').val(type);

        // Change Background
        if (heroImages[type]) {
            $('.hero-section').css('background-image', `url('${heroImages[type]}')`);
        }

        // Ripple Effect
        $(this).css('transform', 'scale(0.95)');
        setTimeout(() => { $(this).css('transform', ''); }, 100);
    });

    // 3. Swap Origin/Destination
    $('.btn-swap').on('click', function () {
        const $origin = $('#origin');
        const $dest = $('#destination');

        const temp = $origin.val();
        $origin.val($dest.val());
        $dest.val(temp);

        // Rotate animation
        $(this).css('transform', 'rotate(180deg)');
        setTimeout(() => { $(this).css('transform', ''); }, 300);
    });

    // 4. Calendar (Flatpickr)
    if ($('#departDate').length) {
        flatpickr("#departDate", {
            dateFormat: "Y-m-d",
            minDate: "today",
            disableMobile: "true",
            onChange: function (selectedDates, dateStr) {
                if (returnPicker) {
                    returnPicker.set('minDate', dateStr);
                }
            }
        });
    }

    let returnPicker;
    if ($('#returnDate').length) {
        returnPicker = flatpickr("#returnDate", {
            dateFormat: "Y-m-d",
            minDate: "today",
            disableMobile: "true"
        });
    }

    // 5. Form Validation
    $('.search-form-row').on('submit', function (e) {
        const origin = $('#origin').val().trim();
        const destination = $('#destination').val().trim();
        const departDate = $('#departDate').val();

        if (origin && destination && origin.toLowerCase() === destination.toLowerCase()) {
            e.preventDefault();
            alert('⚠️ Origin and destination cannot be the same!');
            return false;
        }

        if (!origin || !destination || !departDate) {
            e.preventDefault();
            alert('⚠️ Please fill in all required fields!');
            return false;
        }

        const btn = $(this).find('.btn-search-trip');
        btn.prop('disabled', true);
        btn.html('🔍 Searching...');
    });

    // ------------------------------------------------------------------------
    // D. SITE-WIDE UI ANIMATIONS
    // ------------------------------------------------------------------------

    // Smooth scroll
    $('a[href^="#"]').on('click', function (e) {
        const target = $(this).attr('href');
        if (target === '#') return;
        const targetElement = $(target);
        if (targetElement.length) {
            e.preventDefault();
            $('html, body').animate({
                scrollTop: targetElement.offset().top - 70
            }, 600, 'swing');
        }
    });

    // Stats Animation (About Us)
    const animateStats = () => {
        $('.about-stats').each(function () {
            const elementTop = $(this).offset().top;
            const elementBottom = elementTop + $(this).outerHeight();
            const viewportTop = $(window).scrollTop();
            const viewportBottom = viewportTop + $(window).height();

            if (elementBottom > viewportTop && elementTop < viewportBottom) {
                $(this).find('.stat-number').css('opacity', '1');
            }
        });
    };
    $(window).on('scroll', animateStats);
    animateStats();

    // Flash Message Fade Out
    const infoBox = $('.info');
    if (infoBox.text().trim().length > 0) {
        setTimeout(() => { infoBox.hide(); }, 5500);
    }

    // ========================================================================
    // E. LOGIN PROMPT MODAL LOGIC (New)
    // ========================================================================

    // Function to Open Modal (Globally accessible if needed)
    window.openLoginModal = function () {
        $('#loginModal').css('display', 'flex');
        // Prevent body scroll when modal is open
        $('body').css('overflow', 'hidden');
    };

    // Function to Close Modal
    window.closeLoginModal = function () {
        $('#loginModal').fadeOut(200);
        $('body').css('overflow', ''); // Restore scroll
    };

    // Close on clicking outside the card
    $(document).on('click', function (e) {
        if ($(e.target).is('#loginModal')) {
            closeLoginModal();
        }
    });

    // Close on Escape key
    $(document).on('keydown', function (e) {
        if (e.key === "Escape") {
            closeLoginModal();
        }
    });
});