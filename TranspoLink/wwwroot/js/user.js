// Global variable to store the name we expect
let expectedName = "";

function openDeleteModal(name) {
    expectedName = name;

    // Reset the modal state
    const input = document.getElementById("deleteInput");
    const btn = document.getElementById("confirmDeleteBtn");

    input.value = "";
    btn.disabled = true;
    btn.classList.remove("active");

    // Show the modal
    document.getElementById("deleteModal").style.display = "flex";

    // Focus the input automatically
    input.focus();
}

function closeDeleteModal() {
    document.getElementById("deleteModal").style.display = "none";
}

// Add event listener to the input box
document.addEventListener("DOMContentLoaded", function () {
    const input = document.getElementById("deleteInput");
    const btn = document.getElementById("confirmDeleteBtn");

    if (input && btn) {
        input.addEventListener("input", function () {
            if (this.value === expectedName) {
                // Match! Enable button
                btn.disabled = false;
                btn.classList.add("active");
            } else {
                // No match
                btn.disabled = true;
                btn.classList.remove("active");
            }
        });
    }

    // Allow closing with Escape key
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

    // Read current state from the hidden inputs INSIDE the partial view
    // (If they don't exist yet, fallback to defaults)
    const sort = $('#partialSort').val() || "Id";
    const dir = $('#partialDir').val() || "asc";

    // Visual feedback (dim the table slightly)
    $('#tableContainer').css('opacity', '0.6');

    $.ajax({
        url: '/Admin/Members',
        type: 'GET',
        data: { search: search, page: page, sort: sort, dir: dir },
        success: function (result) {
            // Replace ONLY the table content
            $('#tableContainer').html(result);

            // Restore opacity
            $('#tableContainer').css('opacity', '1');

            // Update visual arrows based on new state
            updateSortIcons();
        },
        error: function () {
            alert("Error loading data. Please try again.");
            $('#tableContainer').css('opacity', '1');
        }
    });
}

// Handle Header Clicks
$(document).on('click', '.sortable', function () {
    const column = $(this).data('col');

    // Get current state
    let currentSort = $('#partialSort').val();
    let currentDir = $('#partialDir').val();

    // Determine new direction:
    // If clicking same column -> toggle direction
    // If clicking new column -> default to asc
    let newDir = 'asc';
    if (currentSort === column) {
        newDir = (currentDir === 'asc') ? 'desc' : 'asc';
    }

    // Update hidden inputs manually so the request uses the new values
    $('#partialSort').val(column);
    $('#partialDir').val(newDir);

    // Reload (Page 1)
    loadTable(1);
});

// Override Search Form Submit
$(document).on('submit', '#searchForm', function (e) {
    e.preventDefault();
    loadTable(1);
});

// Update Arrows UI
function updateSortIcons() {
    const sort = $('#partialSort').val();
    const dir = $('#partialDir').val();

    // Clear all arrows
    $('th.sortable').removeAttr('data-dir');

    // Set arrow for active column
    if (sort) {
        $(`th[data-col="${sort}"]`).attr('data-dir', dir);
    }
}

// Initialize on first load
$(document).ready(function () {
    updateSortIcons();
});