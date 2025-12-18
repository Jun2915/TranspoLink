// RAP.js: Review & Pay 页面逻辑

$(document).ready(function () {
    if (typeof window.bookingData === 'undefined') {
        console.error("Booking data not found in window object.");
        return;
    }

    const data = window.bookingData;
    const $form = $('#reviewForm');

    // 元素选择器
    const $insuranceRadios = $('input[name="HasTravelInsurance"]');
    const $refundCheckbox = $('input[name="HasRefundGuarantee"]');
    const $boardingPassCheckbox = $('#hasBoardingPass');

    // 右侧栏显示元素
    const $insuranceRow = $('.breakdown-row.insurance-row');
    const $refundRow = $('.breakdown-row.refund-row');
    const $boardingPassRow = $('.breakdown-row.boarding-pass-row'); // 对应CSHTML中新增的元素
    const $finalTotalDisplay = $('#finalTotal');
    const $payNowBtnAmount = $('.pay-now-btn .btn-amount');

    // 费用计算函数
    function calculateFinalTotal() {
        const basePricePerTicket = parseFloat(data.basePricePerTicket) || 0;
        const insurancePrice = parseFloat(data.insurancePrice) || 0;
        const refundGuaranteePrice = parseFloat(data.refundGuaranteePrice) || 0;
        const boardingPassPrice = parseFloat(data.boardingPassPrice) || 0;
        const passengerCount = parseInt(data.passengerCount) || 0;

        let total = basePricePerTicket * passengerCount;
        let totalInsurance = 0;
        let totalRefund = 0;
        let totalBoardingPass = 0;
        // 1. 保险费用
        const hasInsurance = $insuranceRadios.filter(':checked').val() === 'true';
        if (hasInsurance) {
            totalInsurance = data.insurancePrice * data.passengerCount;
            total += totalInsurance;
            $insuranceRow.show();
        } else {
            $insuranceRow.hide();
        }

        // 2. 退款保证费用
        const hasRefund = $refundCheckbox.is(':checked');
        if (hasRefund) {
            totalRefund = data.refundGuaranteePrice * data.passengerCount;
            total += totalRefund;
            $refundRow.show();
        } else {
            $refundRow.hide();
        }

        const hasBoardingPass = $boardingPassCheckbox.is(':checked');
        if (hasBoardingPass) {
            totalBoardingPass = boardingPassPrice * passengerCount;
            total += totalBoardingPass;
            $boardingPassRow.show();
        } else {
            $boardingPassRow.hide();
        }

        // 更新细目费用 (确保在 total 累加之后)
        $('#fare-insurance').text(`RM ${totalInsurance.toFixed(2)}`);
        $('#fare-refund').text(`RM ${totalRefund.toFixed(2)}`);
        $('#fare-boardingpass').text(`RM ${totalBoardingPass.toFixed(2)}`);

        // 更新总额显示
        const finalTotal = total.toFixed(2);
        $finalTotalDisplay.text(`RM ${finalTotal}`);
        $payNowBtnAmount.text(`RM ${finalTotal}`);
    }

    // 监听事件
    $insuranceRadios.on('change', calculateFinalTotal);
    $refundCheckbox.on('change', calculateFinalTotal);
    $boardingPassCheckbox.on('change', calculateFinalTotal); // 监听登车费切换

    // 初始计算总额
    calculateFinalTotal();

    // RAP.js 改进建议
    $form.on('submit', function (e) {
        // 1. 强制在提交前执行一次计算，确保金额同步
        calculateFinalTotal();

        // 2. 检查基本必填项（作为辅助）
        let isValid = true;
        $('.rnt-input[required]').each(function () {
            if ($(this).val() === '') {
                isValid = false;
                $(this).addClass('is-invalid');
            }
        });

        if (!isValid) {
            alert("Please fill in all required passenger details.");
            e.preventDefault(); // 如果不合法，阻止跳转
            return false;
        }

        console.log("Form is valid, submitting to ReviewAndPay POST...");
        return true;
    });

});