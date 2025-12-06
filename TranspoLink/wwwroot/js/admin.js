$(document).ready(function () {

    // Override the loadTable function specifically for the Admin Management page
    window.loadTable = function (page) {
        const search = $('#searchInput').val();
        const sort = $('#partialSort').val() || "Id";
        const dir = $('#partialDir').val() || "asc";

        $('#tableContainer').css('opacity', '0.6');

        $.ajax({
            url: '/Admin/Admins',
            type: 'GET',
            data: { search: search, page: page, sort: sort, dir: dir },
            success: function (result) {
                $('#tableContainer').html(result);
                $('#tableContainer').css('opacity', '1');
                updateSortIcons();
            },
            error: function () {
                alert("Error loading data.");
                $('#tableContainer').css('opacity', '1');
            }
        });
    };

    // ============================================================================
    // DASHBOARD DROPDOWN LOGIC (Handles User Mgmt & Transport Ops)
    // ============================================================================

    // Generic function to toggle specific dropdowns
    function setupDropdown(triggerId, menuId) {
        const $trigger = $(triggerId);
        const $menu = $(menuId);

        if ($trigger.length) {
            $trigger.on('click', function (e) {
                e.stopPropagation();

                // Close ANY other open dropdowns first (User experience improvement)
                $('.dropdown-menu-content').not($menu).removeClass('show');

                // Toggle CURRENT one
                $menu.toggleClass('show');
            });
        }
    }

    // Initialize both dropdowns
    setupDropdown('#userMgmtTrigger', '#userDropdown');
    setupDropdown('#opsMgmtTrigger', '#opsDropdown');

    // Close ALL dropdowns when clicking outside
    $(document).on('click', function () {
        $('.dropdown-menu-content').removeClass('show');
    });

});

// ... (Rest of your existing admin.js code for photo preview, modal, etc.) ...
/* =========================================================
CREATE/EDIT ADMIN FORM LOGIC
========================================================= */
$(document).ready(function () {

    // 1. Photo Preview Logic
    $('input[name="Photo"]').on('change', function (e) {
        const file = e.target.files[0];
        const $preview = $('#adminPhotoPreview');

        if (file) {
            if (!file.type.startsWith('image/')) {
                alert("Please select a valid image (JPG or PNG).");
                return;
            }
            const reader = new FileReader();
            reader.onload = function (e) {
                $preview.attr('src', e.target.result);
                $preview.css('border', '3px solid #667eea');
            };
            reader.readAsDataURL(file);
        }
    });

    // 2. Password Toggle Logic
    $('.toggle-password-btn').on('click', function (e) {
        e.preventDefault();
        const $btn = $(this);
        const $input = $btn.siblings('input');
        const currentType = $input.attr('type');
        const newType = currentType === 'password' ? 'text' : 'password';

        $input.attr('type', newType);

        if (newType === 'text') {
            $btn.css('opacity', '1');
        } else {
            $btn.css('opacity', '0.6');
        }
    });
});

/* PHONE INPUT VALIDATION */
$(document).on('input', 'input[name="Phone"]', function () {
    let value = $(this).val();
    if (value.length > 0 && !/^[0-9+\-\s]*$/.test(value)) {
        $(this).addClass('input-validation-error');
    } else {
        $(this).removeClass('input-validation-error');
    }
});

/* DELETE CONFIRMATION MODAL LOGIC */
let deleteUrl = '';

function openConfirmModal(url, name) {
    deleteUrl = url;
    $('#deleteNameDisplay').text(name);
    $('#deleteConfirmForm').attr('action', url);
    $('#confirmModal').css('display', 'flex');
}

function closeConfirmModal() {
    $('#confirmModal').hide();
}

$(window).on('click', function (e) {
    if ($(e.target).is('.confirm-overlay')) {
        closeConfirmModal();
    }
});

$(document).on('keydown', function (e) {
    if (e.key === "Escape") {
        closeConfirmModal();
    }
});