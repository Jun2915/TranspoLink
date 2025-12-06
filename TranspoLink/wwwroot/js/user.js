// ============================================================================
// DELETE MODAL LOGIC
// ============================================================================
let expectedName = "";

function openDeleteModal(name) {
    expectedName = name;
    const input = document.getElementById("deleteInput");
    const btn = document.getElementById("confirmDeleteBtn");

    input.value = "";
    btn.disabled = true;
    btn.classList.remove("active");
    document.getElementById("deleteModal").style.display = "flex";
    input.focus();
}

function closeDeleteModal() {
    document.getElementById("deleteModal").style.display = "none";
}

document.addEventListener("DOMContentLoaded", function () {
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

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            closeDeleteModal();
        }
    });
});

// ============================================================================
// SEARCH CLEAR BUTTON LOGIC
// ============================================================================
function toggleClearButton() {
    const inputVal = $('#searchInput').val();
    const $btn = $('#clearSearchBtn');

    if (inputVal && inputVal.trim() !== '') {
        $btn.css('display', 'inline-flex');
    } else {
        $btn.hide();
    }
}

$(document).ready(function () {
    toggleClearButton();
    $('#searchInput').on('input', function () {
        toggleClearButton();
    });
});

// ============================================================================
// AJAX TABLE SORTING LOGIC
// ============================================================================
window.loadTable = function (page) {
    const search = $('#searchInput').val();
    const sort = $('#partialSort').val() || "Id";
    const dir = $('#partialDir').val() || "asc";

    $('#tableContainer').css('opacity', '0.6');

    $.ajax({
        url: '/Admin/Members',
        type: 'GET',
        data: { search: search, page: page, sort: sort, dir: dir },
        success: function (result) {
            $('#tableContainer').html(result);
            $('#tableContainer').css('opacity', '1');
            updateSortIcons();
        },
        error: function () {
            alert("Error loading data. Please try again.");
            $('#tableContainer').css('opacity', '1');
        }
    });
}

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

$(document).on('submit', '#searchForm', function (e) {
    e.preventDefault();
    loadTable(1);
});

function updateSortIcons() {
    const sort = $('#partialSort').val();
    const dir = $('#partialDir').val();
    $('th.sortable').removeAttr('data-dir');
    if (sort) {
        $(`th[data-col="${sort}"]`).attr('data-dir', dir);
    }
}

$(document).ready(function () {
    updateSortIcons();
});

// ============================================================================
// PROFILE PAGE LOGIC (PREVIEW & EDIT)
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

        // 1. PHOTO CLICK LOGIC (Dual Mode)
        $photoContainer.on('click', function (e) {
            // Check if we are in EDIT mode
            const isEditMode = !$inputs.first().prop('readonly');

            if (!isEditMode) {
                // VIEW MODE: Show Preview Modal
                const currentSrc = $('#profileImagePreview').attr('src');
                $previewTarget.attr('src', currentSrc);
                $previewModal.fadeIn(200).css('display', 'flex');
            }
            else {
                // EDIT MODE: Trigger file input (Handled by photo-tools.js usually, but we ensure it works here)
                // If photo-tools.js handles it, we let it bubble. 
                // But typically photo-tools checks for .disabled-upload class.
            }
        });

        // 2. Handle "Edit User Profile" Click
        $btnEdit.on('click', function () {
            // Enable Inputs
            $inputs.prop('readonly', false);

            // Enable Photo Upload
            $photoInput.prop('disabled', false);

            // Visual feedback
            $photoContainer.removeClass('disabled-upload'); // Enables click in photo-tools.js
            $photoContainer.css('border-color', '#667eea');

            // UI Changes
            $(this).hide();
            $editActions.css('display', 'flex');
            $('.btn-download-icon').hide(); // Hide download button while editing

            $inputs.first().focus();
        });
    }
});

// Close Preview Function (Global scope)
window.closePreview = function () {
    $('#imagePreviewModal').fadeOut(200);
}