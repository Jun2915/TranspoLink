// =========================================================
// CHAT.JS - Real-Time Support Chat (User Widget)
// =========================================================

"use strict";

// 1. Establish Connection
var connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .withAutomaticReconnect()
    .build();

// 2. Select UI Elements
const $chatBox = $('.chat-box-container');
const $chatMessages = $('.chat-messages');
const $input = $('#chatInput');
const $sendBtn = $('#btnSendChat');

// 3. Request Notification Permission
if (Notification.permission !== "granted") {
    Notification.requestPermission();
}

// 4. Helper: Append Message (Matches new CSS structure)
function appendMessage(message, time, isSent, senderName = "You") {
    const typeClass = isSent ? "msg-sent" : "msg-received";

    // For the user widget: 
    // - If sent by me, I usually don't need to see my own name.
    // - If received, I want to see "Support" or the Admin's name.
    const nameHtml = isSent ? "" : `<div class="msg-sender-name">${senderName}</div>`;

    const html = `
        <div class="message-bubble ${typeClass}">
            ${nameHtml}
            <div class="msg-text-content">${message}</div>
            <div class="msg-meta">${time}</div>
        </div>
    `;

    $chatMessages.append(html);
    scrollToBottom();
}

function scrollToBottom() {
    $chatMessages.scrollTop($chatMessages[0].scrollHeight);
}

// 5. SignalR Events

// A. Receive Reply from Admin
connection.on("ReceiveAdminReply", function (adminName, message, time) {
    // Append message to chat area
    appendMessage(message, time, false, adminName + " 🛡️");

    // Play Notification Sound
    try {
        const audio = new Audio('https://codeskulptor-demos.commondatastorage.googleapis.com/pang/pop.mp3');
        audio.volume = 0.5;
        audio.play().catch(e => { });

        // Desktop Notification (if window hidden)
        if (Notification.permission === "granted" && document.hidden) {
            new Notification("Support Reply", {
                body: message,
                icon: "/images/shortcut_icon.png"
            });
        }
    } catch (e) { }

    // Auto-open chat box if it's closed
    if ($chatBox.css('display') === 'none') {
        $chatBox.fadeIn(200);
        scrollToBottom();
    }
});

// B. Confirmation of My Own Message
connection.on("ReceiveMyMessage", function (message, time) {
    appendMessage(message, time, true);
});

// 6. User Actions (Send)
$sendBtn.on('click', function (e) {
    sendMessage();
    e.preventDefault();
});

$input.on('keypress', function (e) {
    if (e.which === 13) {
        sendMessage();
        e.preventDefault();
    }
});

function sendMessage() {
    const msg = $input.val().trim();
    if (msg === "") return;

    connection.invoke("SendMessageToSupport", msg).catch(function (err) {
        return console.error(err.toString());
    });

    $input.val('').focus();
}

// 7. Start Connection
connection.start().then(function () {
    console.log("✅ Chat Connected.");
    $sendBtn.prop('disabled', false);
}).catch(function (err) {
    console.error("❌ SignalR Error: " + err.toString());
});

// 8. Toggle Widget Visibility (Global Function)
window.toggleChat = function () {
    if ($chatBox.css('display') === 'none') {
        $chatBox.css('display', 'flex'); // Must match CSS flex layout
        setTimeout(() => $input.focus(), 100);
        scrollToBottom();
    } else {
        $chatBox.fadeOut(200);
    }
};