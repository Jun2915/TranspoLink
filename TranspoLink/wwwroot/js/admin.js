$(document).ready(function () {

    // ============================================================================
    // ADMIN MANAGEMENT PAGE LOGIC (Search, Sort, AJAX)
    // ============================================================================
    const isAdminPage = window.location.pathname.toLowerCase().includes('/admin/admins');

    if (isAdminPage) {

        // 1. Define loadTable specifically for Admins
        window.loadTable = function (page) {
            const search = $('#searchInput').val();
            const sort = $('#partialSort').val() || "Id";
            const dir = $('#partialDir').val() || "asc";

            $('#tableContainer').css('opacity', '0.6');

            $.ajax({
                url: '/Admin/Admins', // Target the Admin Controller
                type: 'GET',
                data: { search: search, page: page, sort: sort, dir: dir },
                success: function (result) {
                    $('#tableContainer').html(result);
                    $('#tableContainer').css('opacity', '1');
                    updateSortIcons();
                    toggleClearButton(); // Ensure X button state matches input
                },
                error: function () {
                    alert("Error loading data.");
                    $('#tableContainer').css('opacity', '1');
                }
            });
        };

        // 2. Search Input Handler
        $('#searchInput').on('input', function () {
            toggleClearButton();
            loadTable(1);
        });

        // 3. Clear Button Handler
        $('#clearSearchBtn').on('click', function (e) {
            e.preventDefault();
            $('#searchInput').val('');
            toggleClearButton();
            loadTable(1);
        });

        // 4. Sort Click Handler
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

        // Init
        updateSortIcons();
        toggleClearButton();
    }

    // ============================================================================
    // DASHBOARD DROPDOWNS
    // ============================================================================
    function setupDropdown(triggerId, menuId) {
        const $trigger = $(triggerId);
        const $menu = $(menuId);
        if ($trigger.length) {
            $trigger.on('click', function (e) {
                e.stopPropagation();
                $('.dropdown-menu-content').not($menu).removeClass('show');
                $menu.toggleClass('show');
            });
        }
    }
    setupDropdown('#userMgmtTrigger', '#userDropdown');
    setupDropdown('#opsMgmtTrigger', '#opsDropdown');

    $(document).on('click', function () {
        $('.dropdown-menu-content').removeClass('show');
    });

    // ============================================================================
    // FORMS (Create/Edit Admin)
    // ============================================================================
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

    $('.toggle-password-btn').on('click', function (e) {
        e.preventDefault();
        const $btn = $(this);
        const $input = $btn.siblings('input');
        const currentType = $input.attr('type');
        const newType = currentType === 'password' ? 'text' : 'password';
        $input.attr('type', newType);
        $btn.css('opacity', newType === 'text' ? '1' : '0.6');
    });
});

// Helper: Toggle Search Clear Button
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

// Helper: Update Sort Icons
function updateSortIcons() {
    const sort = $('#partialSort').val();
    const dir = $('#partialDir').val();
    $('th.sortable').removeAttr('data-dir');
    if (sort) {
        $(`th[data-col="${sort}"]`).attr('data-dir', dir);
    }
}

// =========================================================
// DELETE CONFIRMATION MODAL
// =========================================================
function openConfirmModal(url, name) {
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