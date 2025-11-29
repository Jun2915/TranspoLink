/* =========================================================
   VEHICLE AJAX SEARCH & SORT
   ========================================================= */

let currentType = "";

// 1. Search Input Handler
$('#searchInput').on('input', function () {
    loadVehicleTable();
});

// 2. Tab Filter Handler
window.filterType = function (type) {
    currentType = type;

    // Update visual tab state
    $('.tab').removeClass('active');
    $(`[data-type="${type}"]`).addClass('active');

    loadVehicleTable();
};

// 3. Sort Click Handler
$(document).on('click', '.sortable', function () {
    const column = $(this).data('col');
    let currentSort = $('#partialSort').val();
    let currentDir = $('#partialDir').val();

    let newDir = 'asc';
    if (currentSort === column) {
        newDir = (currentDir === 'asc') ? 'desc' : 'asc';
    }

    // Store new state temporarily to send
    $('#partialSort').val(column);
    $('#partialDir').val(newDir);

    loadVehicleTable(column, newDir);
});

// 4. Main AJAX Loader
function loadVehicleTable(sortOverride, dirOverride) {
    const search = $('#searchInput').val();
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

// 5. Update Sort Icons (Visual)
function updateSortIcons() {
    const sort = $('#partialSort').val();
    const dir = $('#partialDir').val();
    $('th.sortable').removeAttr('data-dir');
    if (sort) {
        $(`th[data-col="${sort}"]`).attr('data-dir', dir);
    }
}

// Initialize on load
$(document).ready(function () {
    // Other vehicle specific logic (like the Create/Edit form icon)
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
});

document.addEventListener('DOMContentLoaded', function () {
    // Parse the dates passed from Controller
    var tripDates = @Html.Raw(activeDates);

    flatpickr("#vehicleCalendar", {
        inline: true,
        dateFormat: "Y-m-d",
        enable: tripDates, // Only enable dates with trips
        locale: {
            firstDayOfWeek: 1
        },
        onDayCreate: function (dObj, dStr, fp, dayElem) {
            // Add a dot indicator for trip days
            var dateStr = dayElem.dateObj.toISOString().split('T')[0];
            if (tripDates.some(d => d.startsWith(dateStr))) {
                dayElem.innerHTML += "<span class='calendar-dot'></span>";
            }
        }
    });
});