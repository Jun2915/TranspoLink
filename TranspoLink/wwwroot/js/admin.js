$(document).ready(function () {

    // ============================================================================
    // 1. ADMIN MANAGEMENT PAGE LOGIC (Search, Sort, AJAX)
    //    (Combines Admins, Members, and the new Drivers logic)
    // ============================================================================

    // Check if we are on a page that requires the Dynamic Table loader
    const isDynamicTablePage = window.location.pathname.toLowerCase().includes('/admin/admins') ||
        window.location.pathname.toLowerCase().includes('/admin/members') ||
        window.location.pathname.toLowerCase().includes('/routentrip/drivers');

    if (isDynamicTablePage) {

        // Helper to determine the correct AJAX URL based on the current page
        function getAjaxUrl() {
            if (window.location.pathname.toLowerCase().includes('/admin/admins')) return '/Admin/Admins';
            if (window.location.pathname.toLowerCase().includes('/admin/members')) return '/Admin/Members';
            if (window.location.pathname.toLowerCase().includes('/routentrip/drivers')) return '/RouteNTrip/Drivers';
            return '';
        }

        // Define loadTable specifically for the current dynamic page
        window.loadTable = function (page) {
            const search = $('#searchInput').val();
            const sort = $('#partialSort').val() || "Id";
            const dir = $('#partialDir').val() || "asc";
            const ajaxUrl = getAjaxUrl();

            if (!ajaxUrl) return; // Exit if URL not determined

            $('#tableContainer').css('opacity', '0.6');

            $.ajax({
                url: ajaxUrl, // Uses the dynamic URL determined above
                type: 'GET',
                data: { search: search, page: page, sort: sort, dir: dir },
                success: function (result) {
                    $('#tableContainer').html(result);
                    $('#tableContainer').css('opacity', '1');
                    updateSortIcons();
                    toggleClearButton();
                },
                error: function () {
                    alert("Error loading data.");
                    $('#tableContainer').css('opacity', '1');
                }
            });
        };

        // Search Input Handler (Triggers loadTable on typing)
        $('#searchInput').on('input', function () {
            toggleClearButton();
            loadTable(1);
        });

        // Clear Button Handler
        $('#clearSearchBtn').on('click', function (e) {
            e.preventDefault();
            $('#searchInput').val('');
            toggleClearButton();
            loadTable(1);
        });

        // Sort Click Handler
        $(document).on('click', '.sortable', function () {
            const column = $(this).data('col');
            let currentSort = $('#partialSort').val();
            let currentDir = $('#partialDir').val();

            let newDir = 'asc';
            if (currentSort === column) {
                newDir = (currentDir === 'asc') ? 'desc' : 'asc';
            }

            $('#partialSort').val(column);
            $('#partialDir').val(newDir);

            loadTable(1);
        });

        // Initialize UI states on page load
        updateSortIcons();
        toggleClearButton();
    }

    // ============================================================================
    // 2. DASHBOARD DROPDOWNS (User Mgmt & Operations)
    // ============================================================================
    function setupDropdown(triggerId, menuId) {
        const $trigger = $(triggerId);
        const $menu = $(menuId);
        if ($trigger.length) {
            $trigger.on('click', function (e) {
                e.stopPropagation();
                // Close other dropdowns
                $('.dropdown-menu-content').not($menu).removeClass('show');
                // Toggle current
                $menu.toggleClass('show');
            });
        }
    }

    // Initialize specific dropdowns
    setupDropdown('#userMgmtTrigger', '#userDropdown');
    setupDropdown('#opsMgmtTrigger', '#opsDropdown');

    // Close dropdowns when clicking anywhere else
    $(document).on('click', function () {
        $('.dropdown-menu-content').removeClass('show');
    });

    // ============================================================================
    // 3. FORMS (Create/Edit Admin & General Photo Preview)
    // ============================================================================

    // Photo Preview Logic
    $('input[name="Photo"]').on('change', function (e) {
        const file = e.target.files[0];
        const $preview = $('#adminPhotoPreview');
        if (file) {
            if (!file.type.startsWith('image/')) {
                alert("Please select a valid image (JPG or PNG)."); return;
            }
            const reader = new FileReader();
            reader.onload = function (e) {
                $preview.attr('src', e.target.result);
                $preview.css('border', '3px solid #667eea');
            };
            reader.readAsDataURL(file);
        }
    });

    // Password Visibility Toggle
    $('.toggle-password-btn').on('click', function (e) {
        e.preventDefault();
        const $btn = $(this);
        const $input = $btn.siblings('input');
        const currentType = $input.attr('type');
        const newType = currentType === 'password' ? 'text' : 'password';
        $input.attr('type', newType);

        // Adjust opacity to indicate state
        $btn.css('opacity', newType === 'text' ? '1' : '0.6');
    });
});

// ============================================================================
// 4. HELPER FUNCTIONS (Global Scope)
// ============================================================================

// Helper: Toggle Search Clear Button visibility
function toggleClearButton() {
    const inputVal = $('#searchInput').val();
    const $btn = $('#clearSearchBtn');
    if ($btn.length) {
        if (inputVal && inputVal.trim() !== '') {
            $btn.css('display', 'inline-flex');
        } else {
            $btn.hide();
        }
    }
}

// Helper: Update Sort Icons in table headers
function updateSortIcons() {
    const sort = $('#partialSort').val();
    const dir = $('#partialDir').val();
    $('th.sortable').removeAttr('data-dir');
    if (sort) {
        $(`th[data-col="${sort}"]`).attr('data-dir', dir);
    }
}

// =========================================================
// 5. DELETE CONFIRMATION MODAL
// =========================================================

function openConfirmModal(url, name) {
    $('#deleteNameDisplay').text(name);
    $('#deleteConfirmForm').attr('action', url);
    $('#confirmModal').css('display', 'flex');
}

function closeConfirmModal() {
    $('#confirmModal').hide();
}

// Close modal on click outside
$(window).on('click', function (e) {
    if ($(e.target).is('.confirm-overlay')) {
        closeConfirmModal();
    }
});

// Close modal on Escape key
$(document).on('keydown', function (e) {
    if (e.key === "Escape") {
        closeConfirmModal();
    }
});