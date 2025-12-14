// =========================================================
// CHAT.JS - Centralized Chat Logic (Admin & User)
// =========================================================

"use strict";

// 1. Initialize SignalR Connection
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .withAutomaticReconnect()
    .build();

// 2. Shared Variables
let activeUserId = null;       // For Admin: currently selected user
let isHistoryLoaded = false;   // For User: prevents reloading history multiple times
let isSessionActive = true;    // For Admin: tracks if session is ended
let replyContext = "";         // Text being replied to
let pendingFile = null;        // User widget: stores file before sending
let pendingDeleteId = null;    // Admin: Stores ID for the delete modal

// 3. Start Connection & Request Permissions
connection.start().then(function () {
    console.log("✅ Chat Connected.");
    // Enable buttons only after connection is ready
    $('#btnSendChat, #adminSendBtn').prop('disabled', false);
}).catch(function (err) {
    console.error("❌ SignalR Error: " + err.toString());
});

if (Notification.permission !== "granted" && Notification.permission !== "denied") {
    Notification.requestPermission();
}

// =========================================================
// SECTION A: ADMIN DASHBOARD LOGIC
// (Runs only if the Admin Chat Layout is present)
// =========================================================
if ($('.admin-chat-layout').length) {
    console.log("🛡️ Admin Chat Mode Active");

    const $adminMessagesArea = $('#adminMessagesArea');
    const $adminInput = $('#adminInput');
    const $adminSendBtn = $('#adminSendBtn');
    const $endChatBtn = $('#btnEndChat');
    const $userList = $('#userList');

    // A1. Receive Message from User
    connection.on("ReceiveSupportMessage", (email, name, msg, photo, time) => {

        // CHECK IF USER EXISTS IN LIST
        if ($(`[data-user="${email}"]`).length === 0) {
            // FIX: DYNAMICALLY ADD NEW USER TO TOP OF LIST (NO REFRESH)
            const html = `
                <div class="chat-user-item" data-user="${email}" onclick="loadChatHistory('${email}', '${name}')">
                    <div>
                        <div style="font-weight:bold; font-size:14px;">${name}</div>
                        <div style="font-size:12px; color:#666;">${email}</div>
                    </div>
                    <button class="btn-delete-chat" title="Delete Conversation" onclick="openDeleteChatModal(event, '${email}')">
                        🗑️
                    </button>
                </div>`;

            // Insert after the header so it appears at the top
            $userList.find('.user-list-header').after(html);
        }

        // If active chat is this user, render message
        if (activeUserId === email) {
            renderMessage($adminMessagesArea, "User", name, msg, photo, time, false);
            enableAdminInput(); // User replied, re-open session
        } else {
            // Otherwise highlight the user in sidebar
            $(`[data-user="${email}"]`).css('background', '#ffebee');
            playNotification(name, msg);
        }
    });

    // A2. Echo Admin's Own Reply
    connection.on("ReceiveMyReply", (id, msg, photo, time, senderName) => {
        if (id === activeUserId) {
            // Use specific senderName if available, else fallback
            renderMessage($adminMessagesArea, "Me", senderName || "Me", msg, photo, time, false);
        }
    });

    // A3. Function: Load Chat History
    window.loadChatHistory = function (id, name) {
        activeUserId = id;
        $('#chatTitle').text(name);

        // Reset UI
        enableAdminInput();
        $endChatBtn.prop('disabled', false);
        $('.chat-user-item').css('background', '').removeClass('active');
        $(`[data-user="${id}"]`).addClass('active').css('background', '#eef2ff');

        // Fetch History from ChatController
        $.get("/Chat/GetChatHistory?userId=" + encodeURIComponent(id), function (data) {
            $adminMessagesArea.empty();

            if (data.length > 0) {
                data.forEach(m => renderMessage($adminMessagesArea, m.sender, m.name, m.text, m.photo, m.time, m.isAdminSender, m.fullDate));

                // Check if last message was "Session Ended"
                const lastMsg = data[data.length - 1];
                if (lastMsg.text && lastMsg.text.includes("Session Ended")) {
                    disableAdminInput();
                }
            } else {
                $adminMessagesArea.html('<div class="empty-chat-state">No messages yet. Start conversation!</div>');
            }
            scrollToBottom($adminMessagesArea);
        });
    };

    // A4. End Chat
    window.openEndChatModal = function () {
        if (!activeUserId) return;
        $('#chatEndModal').fadeIn(200).css('display', 'flex');
    };

    window.closeEndChatModal = function () {
        $('#chatEndModal').fadeOut(200);
    };

    window.executeEndChat = function () {
        if (!activeUserId) return;

        $.post("/Chat/EndChatSession", { userId: activeUserId }, function (res) {
            if (res.success) {
                $adminMessagesArea.append(`<div class="session-end-marker">— Session Ended by ${res.endedBy} at ${res.time} —</div>`);
                scrollToBottom($adminMessagesArea);
                disableAdminInput();
            }
            closeEndChatModal();
        });
    };

    // A5. Custom Delete Modal Logic
    window.openDeleteChatModal = function (e, id) {
        e.stopPropagation(); // Prevent row click
        pendingDeleteId = id;
        $('#chatDeleteModal').fadeIn(200).css('display', 'flex');
    };

    window.closeDeleteChatModal = function () {
        pendingDeleteId = null;
        $('#chatDeleteModal').fadeOut(200);
    };

    window.executeChatDelete = function () {
        if (!pendingDeleteId) return;

        $.post("/Chat/DeleteConversation", { userId: pendingDeleteId }, function () {
            // Remove from UI
            $(`[data-user="${pendingDeleteId}"]`).fadeOut(300, function () { $(this).remove(); });

            // If deleting the open chat, clear main area
            if (activeUserId === pendingDeleteId) {
                $adminMessagesArea.html('<div class="empty-chat-state">Conversation deleted.</div>');
                $('#chatTitle').text("Select a user");
                activeUserId = null;
                disableAdminInput();
                // Clear session storage so it doesn't reload on refresh
                sessionStorage.removeItem('transpo_chat_id');
                sessionStorage.removeItem('transpo_chat_name');
            }
            closeDeleteChatModal();
        });
    };

    // A6. Send Reply
    function sendAdminReply() {
        const val = $adminInput.val().trim();
        if (val && activeUserId && isSessionActive) {
            connection.invoke("ReplyToUser", activeUserId, val);
            $adminInput.val('');
        }
    }

    $adminSendBtn.on('click', sendAdminReply);
    $adminInput.on('keypress', function (e) { if (e.which === 13) sendAdminReply(); });

    // Helpers
    function disableAdminInput() {
        isSessionActive = false;
        $adminInput.prop('disabled', true).attr('placeholder', 'Session ended. Waiting for user...');
        $adminSendBtn.prop('disabled', true);
        $endChatBtn.prop('disabled', true);
    }

    function enableAdminInput() {
        isSessionActive = true;
        $adminInput.prop('disabled', false).attr('placeholder', 'Type a reply...');
        $adminSendBtn.prop('disabled', false);
        $endChatBtn.prop('disabled', false);
    }

    // --- AUTO-LOAD LAST CHAT ON REFRESH ---
    const lastId = sessionStorage.getItem('transpo_chat_id');
    const lastName = sessionStorage.getItem('transpo_chat_name');

    if (lastId && lastName) {
        setTimeout(() => {
            if ($(`[data-user="${lastId}"]`).length) {
                loadChatHistory(lastId, lastName);
            }
        }, 100);
    }
}

// =========================================================
// SECTION B: USER WIDGET LOGIC
// (Runs only if the User Chat Box is present)
// =========================================================
if ($('.chat-box-container').length) {
    console.log("👤 User Chat Mode Active");

    const $userBox = $('.chat-box-container');
    const $userMessages = $('.chat-messages');
    const $userInput = $('#chatInput');
    const $userSendBtn = $('#btnSendChat');
    const $imgPreviewBar = $('#imagePreviewBar');
    const $imgPreviewSrc = $('#imagePreviewSrc');

    // B1. Receive Reply from Admin
    connection.on("ReceiveAdminReply", function (adminName, message, photo, time) {
        // Show specific admin name with shield
        renderMessage($userMessages, "Support", adminName + " 🛡️", message, photo, time, false); // false = User View
        playNotification("Support", message);

        if ($userBox.css('display') === 'none') {
            $userBox.fadeIn(200);
        }
    });

    // B2. Echo Own Message
    connection.on("ReceiveMyMessage", function (message, photo, time) {
        renderMessage($userMessages, "Me", "You", message, photo, time, false);
    });

    // B3. Toggle & History
    window.toggleChat = function () {
        if ($userBox.css('display') === 'none') {
            $userBox.css('display', 'flex');
            setTimeout(() => $userInput.focus(), 100);

            if (!isHistoryLoaded) {
                // Fetch from ChatController
                $.get("/Chat/GetMyChatHistory", function (data) {
                    $userMessages.find('.loading-msg').remove();
                    data.forEach(m => {
                        renderMessage($userMessages, m.sender, m.name, m.text, m.photo, m.time, false, m.fullDate)
                    });
                    isHistoryLoaded = true;
                    scrollToBottom($userMessages);
                });
            }
            scrollToBottom($userMessages);
        } else {
            $userBox.fadeOut(200);
        }
    };

    // B4. Select Photo & Preview
    window.triggerPhotoUpload = () => $('#chatPhotoInput').click();

    window.previewPhoto = function (el) {
        if (el.files && el.files[0]) {
            pendingFile = el.files[0];
            const reader = new FileReader();
            reader.onload = function (e) {
                $imgPreviewSrc.attr('src', e.target.result);
                $imgPreviewBar.slideDown(200);
                $userInput.focus();
            }
            reader.readAsDataURL(pendingFile);
        }
    };

    window.cancelImage = function () {
        pendingFile = null;
        $('#chatPhotoInput').val('');
        $imgPreviewBar.slideUp(200);
    };

    // B5. Send Message (Handles Text & Photo via AJAX)
    $userSendBtn.on('click', async function () {
        const text = $userInput.val().trim();

        // Case 1: Uploading Image + Optional Text
        if (pendingFile) {
            $userSendBtn.prop('disabled', true).text('...');
            try {
                const formData = new FormData();
                formData.append("file", pendingFile);

                // Post to ChatController
                const response = await fetch('/Chat/UploadPhoto', { method: 'POST', body: formData });
                const result = await response.json();

                if (result.success) {
                    // Send via SignalR after upload
                    connection.invoke("SendMessageToSupport", text, result.url, replyContext);
                    $userInput.val('');
                    cancelImage();
                    cancelReply();
                } else {
                    alert("Upload failed.");
                }
            } catch (err) {
                alert("Error uploading image.");
            } finally {
                $userSendBtn.prop('disabled', false).text('➤');
            }
        }
        // Case 2: Text Only
        else if (text) {
            connection.invoke("SendMessageToSupport", text, null, replyContext);
            $userInput.val('');
            cancelReply();
        }
    });

    $userInput.on('keypress', function (e) { if (e.which === 13) $userSendBtn.click(); });

    // B6. Reply UI
    window.setReply = function (text) {
        replyContext = text;
        $('#replyTextTarget').text(`Replying to: "${text.substring(0, 15)}..."`);
        $('#replyPreviewBar').css('display', 'flex');
        $userInput.focus();
    };
    window.cancelReply = function () {
        replyContext = "";
        $('#replyPreviewBar').hide();
    };
}

// =========================================================
// SHARED RENDER & UTILS
// =========================================================
function renderMessage(container, type, name, text, photo, time, isAdminSender, fullDate) {

    // Check for System End Message
    if (text && (text.includes("--- Session Ended") || name === "System")) {
        const displayText = text.replace(/---/g, '').trim();
        const displayTime = fullDate || time;
        container.append(`<div class="session-end-marker">${displayText} at ${displayTime}</div>`);
        scrollToBottom(container);
        return;
    }

    const isMe = type === "Me";
    let cls = isMe ? "msg-sent" : "msg-received";
    // Purple box for other admins in Admin View Only
    if (isAdminSender && !isMe && $('.admin-chat-layout').length) cls = "msg-admin-internal";

    let contentHtml = "";

    // Parse Reply Context
    if (text && text.startsWith("[Replying to:")) {
        const split = text.indexOf("]\n");
        if (split > -1) {
            const context = text.substring(14, split - 1);
            text = text.substring(split + 2);
            contentHtml += `<div class="reply-context">${context}</div>`;
        }
    }

    // Render Photo
    if (photo) {
        const imgSrc = photo.startsWith("/") ? photo : "/images/" + photo;
        contentHtml += `<a href="${imgSrc}" target="_blank"><img src="${imgSrc}" class="chat-photo-img"></a>`;
    }

    // Text
    if (text) contentHtml += `<div class="msg-text-content">${text}</div>`;

    // Click to Reply (Enabled for User Widget)
    const replyAttr = (!isMe && $('.chat-box-container').length) ? `onclick="setReply('${text.replace(/'/g, "\\'")}')" style="cursor:pointer" title="Click to reply"` : "";

    const html = `
        <div class="message-bubble ${cls}" ${replyAttr}>
            <div class="msg-sender-name">${name}</div>
            ${contentHtml}
            <div class="msg-meta">${time}</div>
        </div>`;

    container.append(html);
    scrollToBottom(container);
}

function scrollToBottom(el) {
    if (el.length) el.scrollTop(el[0].scrollHeight);
}

function playNotification(name, body) {
    try {
        const audio = new Audio('https://codeskulptor-demos.commondatastorage.googleapis.com/pang/pop.mp3');
        audio.volume = 0.5;
        audio.play().catch(e => { });
    } catch (e) { }

    if (Notification.permission === "granted" && document.hidden) {
        new Notification(`New message from ${name}`, {
            body: body || "You have a new message",
            icon: "/images/shortcut_icon.png"
        });
    }
}