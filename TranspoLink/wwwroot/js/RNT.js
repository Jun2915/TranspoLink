// RNT.js - Route & Trip Logic

$(function () {

    // *** CRITICAL FIX: Use a jQuery listener to ensure the function fires ***
    $('#btnSaveTripStatus').on('click', function () {
        const tripId = $(this).data('tripId');
        saveTripStatus(tripId);
    });
    // ********************************************************************

    window.saveTripStatus = function (tripId) {
        const newStatus = $('#overallStatus').val();
        const token = $('input[name="__RequestVerificationToken"]').val();

        if (!newStatus) {
            alert("Please select a status.");
            return;
        }

        $.post('/RouteNTrip/UpdateTripStatus', {
            id: tripId,
            status: newStatus,
            __RequestVerificationToken: token
        }, function (res) {
            if (res.success) {
                alert('SUCCESS: Trip status updated to ' + newStatus + '!');
                location.reload();
            } else {
                alert('FAIL: Could not update status. Reason: ' + (res.message || 'Unknown server error.'));
            }
        }).fail(function (jqXHR, textStatus, errorThrown) {
            console.error("AJAX CRITICAL ERROR:", textStatus, errorThrown, jqXHR.responseText);
            alert("CRITICAL ERROR: Failed to communicate with the server. Check console for Network tab details.");
        });
    };



    $('.update-ata-btn').on('click', function () {
        const row = $(this).closest('tr');
        const id = $(this).data('id');
        const timeVal = row.find('.ata-input').val();
        const statusVal = row.find('.status-select').val();

        const token = $('input[name="__RequestVerificationToken"]').val();

        $.post('/RouteNTrip/UpdateTripStop', {
            id: id,
            actualTime: timeVal,
            status: statusVal,
            __RequestVerificationToken: token
        }, function (res) {
            if (res.success) {
                alert('Stop updated successfully!');
                row.css('background', '#eafff3');
            } else {
                alert('Update failed.');
            }
        });
    });

    $('.rnt-tab').on('click', function (e) {
        e.preventDefault(); 

        const url = $(this).attr('href');
        const type = $(this).text().trim();

        $('.rnt-tab').removeClass('active');
        $(this).addClass('active');

        window.location.href = url;
    });
});