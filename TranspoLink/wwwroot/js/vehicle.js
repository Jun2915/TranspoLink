/* =========================================================
   VEHICLE AJAX SEARCH & SORT & INITIALIZATION (FINAL CHECK)
   ========================================================= */

let currentType = "";

// 1. Main AJAX Loader
function loadVehicleTable(sortOverride, dirOverride) {
    // CRITICAL: Check if searchInput exists before trying to get its value.
    const searchInput = $('#searchInput');
    const search = searchInput.length ? searchInput.val() : '';

    const sort = sortOverride || $('#partialSort').val() || "Id";
    const dir = dirOverride || $('#partialDir').val() || "asc";

    $('#tableContainer').css('opacity', '0.6');

    $.ajax({
        url: '/Vehicle/Vehicles',
        type: 'GET',
        data: {
            search: search,
            type: currentType,
            sort: sort,
            dir: dir
        },
        // ... success and error handlers remain the same ...
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
}

// 2. Update Sort Icons (Visual) - stays the same
function updateSortIcons() {
    const sort = $('#partialSort').val();
    const dir = $('#partialDir').val();
    $('th.sortable').removeAttr('data-dir');
    if (sort) {
        $(`th[data-col="${sort}"]`).attr('data-dir', dir);
    }
}


// 3. Document Ready (Initialization and Event Binding)
// Use the safer jQuery function closure
(function ($) {
    $(document).ready(function () {

        // --- VEHICLE FILTER TAB HANDLER (FINAL FIX) ---
        // Bind the click handler to the rnt-tab class
        $('.rnt-tab').on('click', function (e) {
            e.preventDefault();

            const type = $(this).data('type');
            currentType = type;

            // Update visual tab state 
            $('.rnt-tab').removeClass('active');
            $(this).addClass('active');

            loadVehicleTable();
        });


        // --- SEARCH HANDLER ---
        $('#searchInput').on('input', function () {
            loadVehicleTable();
        });

        // --- SORT HANDLER ---
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

            loadVehicleTable(column, newDir);
        });

        // --- UTILITY/INITIALIZATION LOGIC ---
        const typeSelect = $('#vehicleTypeSelect');
        const iconSpan = $('#typeIcon');

        if (typeSelect.length) {
            typeSelect.on('change', function () {
                updateIcon($(this).val());
            });
            updateIcon(typeSelect.val());
        }

        function updateIcon(type) {
            let icon = "🚌";
            if (type === "Train") icon = "🚄";
            if (type === "Ferry") icon = "🚢";
            iconSpan.text(icon);
        }

        // Auto Uppercase
        $('input[name="VehicleNumber"]').on('input', function () {
            $(this).val($(this).val().toUpperCase());
        });

        // Initial run to set sort icons and display data
        updateSortIcons();
    });
})(jQuery);