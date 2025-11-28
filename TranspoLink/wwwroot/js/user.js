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
// PROFILE PAGE LOGIC (EDIT MODE & INLINE BUTTONS)
// ============================================================================

$(document).ready(function () {
    const $profileForm = $('#profileForm');

    // Only run this logic if we are on the profile page
    if ($profileForm.length) {
        const $btnEdit = $('#btnEditProfile');
        const $editActions = $('#editActions');
        const $inputs = $('.field-input');

        // Updated Selectors for the new structure
        const $photoContainer = $('#photoContainer');
        const $photoInput = $('input[name="Photo"]');
        const $editOverlay = $('.edit-overlay');

        // 1. Handle "Edit User Profile" Click
        $btnEdit.on('click', function () {
            // Enable Inputs
            $inputs.prop('readonly', false);

            // Enable Photo Upload (Logic handled in photo-tools.js via click on container)
            $photoInput.prop('disabled', false);

            // Visual feedback
            $photoContainer.removeClass('disabled-upload');
            $photoContainer.css('cursor', 'pointer');
            $photoContainer.css('border-color', '#667eea');
            $editOverlay.show(); // Show the little pen icon

            // UI Changes: Hide Edit button, Show Save/Cancel Buttons
            $(this).hide();
            $editActions.css('display', 'flex');

            // Focus on the first input
            $inputs.first().focus();
        });
    }
});