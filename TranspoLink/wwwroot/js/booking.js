$(document).ready(function () {
    if (typeof window.selectedSeats === 'undefined') {
        window.selectedSeats = [];
    }

    const pricePerSeat = parseFloat($('.price-per-seat').data('price'));
    const $seatError = $('#seatError');
    const $numSeatsDisplay = $('#numSeats');
    const $selectedSeatsDisplay = $('#selectedSeatsDisplay');
    const $estimatedTotalDisplay = $('#estimatedTotal');
    const $formSelectedSeats = $('#formSelectedSeats');

    function updateSummary() {
        const numSeats = window.selectedSeats.length;
        const totalFare = (numSeats * pricePerSeat).toFixed(2);
        const seatsString = window.selectedSeats.join(', ');

        $numSeatsDisplay.text(numSeats);
        $selectedSeatsDisplay.text(seatsString);
        // 简单的货币格式化
        $estimatedTotalDisplay.text(`MYR ${totalFare.replace(/\B(?=(\d{3})+(?!\d))/g, ",")}`);

        if (numSeats > 0) {
            $seatError.hide();
        } else {
            $seatError.text("Please select at least one seat.").show();
        }
    }

    function updateSeatMapUI() {
        $('.seat-item').each(function () {
            const seat = $(this).data('seat');
            if (window.selectedSeats.includes(seat)) {
                $(this).addClass('selected');
            } else {
                $(this).removeClass('selected');
            }
        });
    }

    // 监听座位点击
    $(document).on('click', '.seat-available', function () {
        const $seatItem = $(this);
        const seat = $seatItem.data('seat');
        const isAvailable = $seatItem.data('available') === true;

        if (!isAvailable) {
            return;
        }

        // 切换选择状态
        if (window.selectedSeats.includes(seat)) {
            window.selectedSeats = window.selectedSeats.filter(s => s !== seat);
        } else {
            window.selectedSeats.push(seat);
        }

        updateSeatMapUI();
        updateSummary();
    });

    $('#seatForm').on('submit', function (e) {
        const seatsString = window.selectedSeats.join(',');
        $formSelectedSeats.val(seatsString); // 关键：将数组写入隐藏字段

        if (window.selectedSeats.length === 0) {
            $seatError.text("Please select at least one seat.").show();
            e.preventDefault();
            return false;
        }
        return true;
    });

    // 初始加载
    updateSeatMapUI();
    updateSummary();
});