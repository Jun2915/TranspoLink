$(document).ready(function () {
    // --- 功能 1：处理取消订单的确认逻辑 --- 
    $(document).on('submit', '.cancel-form', function (e) {
        const confirmation = confirm("Are you sure you want to cancel this booking?\n\n" +
            "Note: The refund money will be processed in 1-3 working days.");

        if (!confirmation) {
            e.preventDefault();
            return false;
        }
    });

    // --- 功能 2：自动隐藏退款提示框 --- 
    const refundAlert = $('.refund-alert, .alert-warning');
    if (refundAlert.length > 0) {
        setTimeout(function () {
            refundAlert.fadeOut(500, function () {
                $(this).remove();
            });
        }, 3000);
    }
});

// --- 功能 3：点击 Details 弹出小卡片逻辑 ---
function showDetails(bookingId, bookingRef) {

    $('#modalRef').text(bookingRef);

    const $list = $('#passengerList');
    $list.html('<div class="text-center p-2"><div class="spinner-border spinner-border-sm text-primary"></div></div>');

    var detailsModal = new bootstrap.Modal(document.getElementById('detailsModal'));
    detailsModal.show();

    $.ajax({
        url: '/Booking/GetBookingDetails',
        type: 'GET',
        data: { id: bookingId },
        success: function (data) {
            $list.empty();
            if (data && data.length > 0) {
                // 循环渲染仅显示名字
                data.forEach(function (passenger) {
                    $list.append(`
                        <div class="list-group-item border-0 px-0 py-1 d-flex align-items-center">
                            <i class="bi bi-person-fill text-secondary me-2"></i>
                            <span class="fw-bold text-dark">${passenger.name}</span>
                        </div>
                    `);
                });
            } else {
                $list.html('<div class="text-muted small">No details found.</div>');
            }
        },
        error: function () {
            $list.html('<div class="text-danger small">Error loading data.</div>');
        }
    });
}