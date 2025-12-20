$(document).ready(function () {
    const cardInput = $('#cardNumber');
    const expiryInput = $('#expiry');
    const cvvInput = $('#cvv');
    const payBtn = $('#finalPayBtn');

    function generateRandomQR() {
        const container = document.getElementById("qrcode-container");
        if (!container) return;

        container.innerHTML = "";

       
        const randomPayload = "TNG_PAY_" + Math.random().toString(36).substr(2, 9).toUpperCase();

        new QRCode(container, {
            text: randomPayload,
            width: 160,
            height: 160,
            colorDark: "#000000",
            colorLight: "#ffffff",
            correctLevel: QRCode.CorrectLevel.H
        });
    }


    cardInput.on('input', function () {
        let val = $(this).val().replace(/\D/g, '');
        $(this).val(val.replace(/(\d{4})(?=\d)/g, '$1 '));
    });

    expiryInput.on('input', function () {
        let val = $(this).val().replace(/\D/g, '');
        if (val.length >= 2) {
            $(this).val(val.slice(0, 2) + '/' + val.slice(2, 4));
        }
    });

    cvvInput.on('input', function () {
        $(this).val($(this).val().replace(/\D/g, '').slice(0, 3));
    });

    $('input[name="payMethod"]').change(function () {
        const method = $(this).val();
        const payBtn = $('#finalPayBtn');

        if (method === 'TNG') {
            $('#card-fields').hide();
            $('#tng-fields').fadeIn(300, function () {
            
                generateRandomQR();

                setTimeout(function () {
                    if ($('input[name="payMethod"]:checked').val() === 'TNG') {
                        console.log("Scan detected! Auto-confirming payment...");

                        // 改变按钮状态以提示用户
                        payBtn.prop('disabled', true).text("Scan Detected! Processing...");

                        // 直接调用支付处理函数
                        processOrder();
                    }
                }, 5000); 
            });
        } else {
            $('#card-fields').fadeIn();
            $('#tng-fields').hide();
          
            payBtn.prop('disabled', false).text("Confirm Payment");
        }
    });

 
    function processOrder() {
        const payBtn = $('#finalPayBtn');
        payBtn.prop('disabled', true).text("Processing...");

        $.post('/Booking/ProcessPayment', function (res) {
            if (res.success) {
                alert("Payment Successful! Scan recognized.");
                window.location.href = '/Home/Index';
            } else {
                alert("Payment failed: " + res.message);
                payBtn.prop('disabled', false).text("Confirm Payment");
            }
        });
    }

   
    $('#finalPayBtn').click(function () {
        const method = $('input[name="payMethod"]:checked').val();
        if (method === 'Card') {
            
            if ($('#cardNumber').val().replace(/\s/g, '').length !== 16) {
                alert("Please enter a valid 16-digit card number.");
                return;
            }
            processOrder();
        } else if (method === 'TNG') {
            
            processOrder();
        }
    });
});