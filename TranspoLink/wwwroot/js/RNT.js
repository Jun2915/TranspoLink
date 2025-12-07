// RNT.js - Route & Trip Logic

$(document).ready(function () {

    // Auto-update ATA (Actual Time of Arrival)
    $('.update-ata-btn').on('click', function () {
        const row = $(this).closest('tr');
        const id = $(this).data('id');
        const timeVal = row.find('.ata-input').val();
        const statusVal = row.find('.status-select').val();

        $.post('/RouteNTrip/UpdateTripStop', {
            id: id,
            actualTime: timeVal,
            status: statusVal
        }, function (res) {
            if (res.success) {
                alert('Stop updated successfully!');
                row.css('background', '#eafff3');
            }
        });
    });

    // Simple Tab Filter for Routes/Trips
    window.filterRNT = function (type, url) {
        $('.rnt-tab').removeClass('active');
        $(event.target).addClass('active');

        $.get(url, { type: type, status: type }, function (data) {
            // Assuming the view returns a partial or we reload content
            // For simplicity in this version, we might just reload page with query param
            window.location.href = url + '?type=' + type + '&status=' + type;
        });
    };
});