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

 
    function processOrder(expiryDate = "") {
        const payBtn = $('#finalPayBtn');
        payBtn.prop('disabled', true).text("Processing...");

        // 向后端发送 expiryDate 进行二次校验
        $.post('/Booking/ProcessPayment', { expiryDate: expiryDate }, function (res) {
            if (res.success) {
                alert("Payment Successful!");
                window.location.href = '/Home/Index';
            } else {
                alert("Payment failed: " + res.message);
                // 如果后端返回过期标志，则刷新页面
                if (res.isExpired) {
                    window.location.reload();
                } else {
                    payBtn.prop('disabled', false).text("Confirm Payment");
                }
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
            const expiryVal = $('#expiry').val(); // 格式为 MM/YY
            if (!/^\d{2}\/\d{2}$/.test(expiryVal)) {
                alert("Please enter expiry date in MM/YY format.");
                return;
            }

            const parts = expiryVal.split('/');
            const month = parseInt(parts[0], 10);
            const year = parseInt("20" + parts[1], 10); // 补全为 20XX 年

            // 验证月份合法性
            if (month < 1 || month > 12) {
                alert("Invalid month! Please enter 01-12.");
                return;
            }

            // 验证是否过期
            const now = new Date();
            const currentMonth = now.getMonth() + 1;
            const currentYear = now.getFullYear();

            if (year < currentYear || (year === currentYear && month < currentMonth)) {
                alert("The card has expired. Please use a valid card.");
                // 如果你希望在过期时刷新页面，取消下面这一行的注释：
                // window.location.reload(); 
                return;
            }

            processOrder(expiryVal); // 将有效期传给处理函数
        } else if (method === 'TNG') {
            processOrder();
        }
    });
           
});