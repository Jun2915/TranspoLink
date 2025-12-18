$(document).ready(function () {
    // 1. 切换支付方式
    $('input[name="payType"]').change(function () {
        if (this.value === 'TNG') {
            $('#tng-qr').show();
            $('#card-form').hide();
            // 模拟扫码成功
            setTimeout(() => {
                $('#tng-status').text("✔ Scan Successful! Processing...");
                setTimeout(processOrder, 2000);
            }, 5000);
        } else {
            $('#tng-qr').hide();
            $('#card-form').show();
        }
    });

    // 2. 卡号格式化 (每4位加空格)
    $('#cardNum').on('input', function () {
        let v = $(this).val().replace(/\s+/g, '').replace(/[^0-9]/gi, '');
        let matches = v.match(/\d{4,16}/g);
        let match = matches && matches[0] || '';
        let parts = [];
        for (let i = 0, len = match.length; i < len; i += 4) { parts.push(match.substring(i, i + 4)); }
        if (parts.length) { $(this).val(parts.join(' ')); }
    });

    // 3. 点击支付按钮
    $('#btnPay').click(function () {
        if ($('input[name="payType"]:checked').val() === 'Card') {
            if ($('#cardNum').val().length < 19) { alert("Invalid Card Number"); return; }
        }
        processOrder();
    });

    function processOrder() {
        $.post('/Booking/ProcessPayment', function (res) {
            if (res.success) {
                alert("Payment Successful!");
                window.location.href = '/Booking/BookingList';
            }
        });
    }
});