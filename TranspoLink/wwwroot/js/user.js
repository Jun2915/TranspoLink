// ============================================================================
// GLOBAL HELPERS (Available everywhere)
// ============================================================================

// 1. DELETE MODAL (Complex - Type Name to Confirm)
// Used in: MemberDetails, AdminDetails
let expectedName = "";

function openDeleteModal(name) {
    expectedName = name;
    const input = document.getElementById("deleteInput");
    const btn = document.getElementById("confirmDeleteBtn");
    const modal = document.getElementById("deleteModal");

    if (input) {
        input.value = "";
        // Reset button state
        if (btn) {
            btn.disabled = true;
            btn.classList.remove("active");
        }
    }

    if (modal) {
        modal.style.display = "flex";
        if (input) setTimeout(() => input.focus(), 100);
    }
}

function closeDeleteModal() {
    const modal = document.getElementById("deleteModal");
    if (modal) modal.style.display = "none";
}

// 2. CONFIRM MODAL (Simple - Yes/No)
// Used in: Member Table, Admin Table
function openConfirmModal(url, name) {
    $('#deleteNameDisplay').text(name);
    $('#deleteConfirmForm').attr('action', url);
    $('#confirmModal').css('display', 'flex');
}

function closeConfirmModal() {
    $('#confirmModal').hide();
}

// Global Event Listeners for Modals
document.addEventListener("DOMContentLoaded", function () {
    // Delete Input Logic
    const input = document.getElementById("deleteInput");
    const btn = document.getElementById("confirmDeleteBtn");

    if (input && btn) {
        input.addEventListener("input", function () {
            if (this.value === expectedName) {
                btn.disabled = false;
                btn.classList.add("active");
            } else {
                btn.disabled = true;
                btn.classList.remove("active");
            }
        });
    }

    // Close on Escape key
    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            closeDeleteModal();
            closeConfirmModal();
        }
    });

    // Close on clicking outside
    $(window).on('click', function (e) {
        if ($(e.target).is('.confirm-overlay')) {
            closeConfirmModal();
        }
        if ($(e.target).is('#deleteModal')) {
            closeDeleteModal();
        }
    });
});


// ============================================================================
// MEMBER MANAGEMENT PAGE LOGIC
// Only runs on /Admin/Members to avoid conflicts with Admin/Vehicles pages
// ============================================================================
$(document).ready(function () {

    // Check if we are on the Members page
    const isMemberPage = window.location.pathname.toLowerCase().includes('/admin/members');

    if (isMemberPage) {

        // 1. Define loadTable specifically for Members
        window.loadTable = function (page) {
            const search = $('#searchInput').val();
            const sort = $('#partialSort').val() || "Id";
            const dir = $('#partialDir').val() || "asc";

            $('#tableContainer').css('opacity', '0.6');

            $.ajax({
                url: '/Admin/Members', // Points correctly to Members
                type: 'GET',
                data: { search: search, page: page, sort: sort, dir: dir },
                success: function (result) {
                    $('#tableContainer').html(result);
                    $('#tableContainer').css('opacity', '1');
                    updateSortIcons();
                    toggleClearButton(); // Re-check button state
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

            // Update hidden inputs
            $('#partialSort').val(column);
            $('#partialDir').val(newDir);

            loadTable(1);
        });

        // Initialize icons
        updateSortIcons();
        toggleClearButton();
    }
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

// ============================================================================
// PROFILE EDIT LOGIC (Existing code preserved)
// ============================================================================
$(document).ready(function () {
    const $profileForm = $('#profileForm');
    if ($profileForm.length) {
        const $btnEdit = $('#btnEditProfile');
        const $editActions = $('#editActions');
        const $inputs = $('.field-input');
        const $photoContainer = $('#photoContainer');
        const $photoInput = $('input[name="Photo"]');
        const $previewModal = $('#imagePreviewModal');
        const $previewTarget = $('#previewImgTarget');

        $photoContainer.on('click', function (e) {
            const isEditMode = !$inputs.first().prop('readonly');
            if (!isEditMode) {
                const currentSrc = $('#profileImagePreview').attr('src');
                $previewTarget.attr('src', currentSrc);
                $previewModal.fadeIn(200).css('display', 'flex');
            }
        });

        $btnEdit.on('click', function () {
            $inputs.prop('readonly', false);
            $photoInput.prop('disabled', false);
            $photoContainer.removeClass('disabled-upload');
            $photoContainer.css('border-color', '#667eea');
            $(this).hide();
            $editActions.css('display', 'flex');
            $('.btn-download-icon').hide();
            $inputs.first().focus();
        });
    }
});

window.closePreview = function () {
    $('#imagePreviewModal').fadeOut(200);
}