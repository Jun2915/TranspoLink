$(document).ready(function () {

    // Override the loadTable function specifically for the Admin Management page
    window.loadTable = function (page) {
        const search = $('#searchInput').val();
        const sort = $('#partialSort').val() || "Id";
        const dir = $('#partialDir').val() || "asc";

        $('#tableContainer').css('opacity', '0.6');

        $.ajax({
            url: '/Admin/Admins', // Points to the Admin controller
            type: 'GET',
            data: { search: search, page: page, sort: sort, dir: dir },
            success: function (result) {
                $('#tableContainer').html(result);
                $('#tableContainer').css('opacity', '1');
                updateSortIcons(); // This helper function from user.js is still available
            },
            error: function () {
                alert("Error loading data.");
                $('#tableContainer').css('opacity', '1');
            }
        });
    };

});

/* =========================================================
CREATE/EDIT ADMIN FORM LOGIC
========================================================= */
$(document).ready(function () {

    // 1. Photo Preview Logic
    // Listen for file selection on the specific input
    $('input[name="Photo"]').on('change', function (e) {
        const file = e.target.files[0];
        const $preview = $('#adminPhotoPreview'); // Ensure ID matches View

        if (file) {
            // Basic check to ensure it's an image
            if (!file.type.startsWith('image/')) {
                alert("Please select a valid image (JPG or PNG).");
                return;
            }

            const reader = new FileReader();
            reader.onload = function (e) {
                $preview.attr('src', e.target.result);
                $preview.css('border', '3px solid #667eea'); // Highlight border on change
            };
            reader.readAsDataURL(file);
        }
    });

    // 2. Password Toggle Logic
    $('.toggle-password-btn').on('click', function (e) {
        e.preventDefault(); // Prevent form submit
        const $btn = $(this);
        const $input = $btn.siblings('input');
        const currentType = $input.attr('type');
        const newType = currentType === 'password' ? 'text' : 'password';

        $input.attr('type', newType);

        // Optional: Change icon opacity or swap SVG path if you want advanced visuals
        if (newType === 'text') {
            $btn.css('opacity', '1'); // Active state
        } else {
            $btn.css('opacity', '0.6'); // Inactive state
        }
    });

});

/* =========================================================
PHONE INPUT VALIDATION (Blocks letters visually)
========================================================= */

$(document).on('input', 'input[name="Phone"]', function () {
    let value = $(this).val();
    // Allow only numbers, spaces, dashes, and plus sign
    // If it contains anything else (like letters), turn RED
    if (value.length > 0 && !/^[0-9+\-\s]*$/.test(value)) {
        $(this).addClass('input-validation-error'); // Uses your new CSS red border
    } else {
        $(this).removeClass('input-validation-error');
    }
});

/* =========================================================
   DELETE CONFIRMATION MODAL LOGIC
   ========================================================= */
let deleteUrl = '';

function openConfirmModal(url, name) {
    deleteUrl = url;

    // Update the modal text
    $('#deleteNameDisplay').text(name);

    // Set the form action
    $('#deleteConfirmForm').attr('action', url);

    // Show the modal
    $('#confirmModal').css('display', 'flex');
}

function closeConfirmModal() {
    $('#confirmModal').hide();
}

// Close if clicking outside the box
$(window).on('click', function (e) {
    if ($(e.target).is('.confirm-overlay')) {
        closeConfirmModal();
    }
});

// Close on Escape key
$(document).on('keydown', function (e) {
    if (e.key === "Escape") {
        closeConfirmModal();
    }
});