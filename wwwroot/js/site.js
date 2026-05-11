document.addEventListener("DOMContentLoaded", function () {
    const revealItems = document.querySelectorAll("[data-reveal]");

    if ("IntersectionObserver" in window) {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (!entry.isIntersecting) {
                    return;
                }

                entry.target.classList.add("is-visible");
                observer.unobserve(entry.target);
            });
        }, {
            threshold: 0.15
        });

        revealItems.forEach((item) => observer.observe(item));
    } else {
        revealItems.forEach((item) => item.classList.add("is-visible"));
    }

    const notificationLink = document.querySelector("[data-notification-link]");
    const notificationBadge = document.querySelector("[data-notification-badge]");
    let unreadNotificationCount = Number(notificationLink?.dataset.notificationCount || notificationBadge?.textContent || 0);

    const ensureToastStack = () => {
        let stack = document.querySelector("[data-notification-toast-stack]");
        if (!stack) {
            stack = document.createElement("div");
            stack.className = "notification-toast-stack";
            stack.setAttribute("data-notification-toast-stack", "");
            document.body.appendChild(stack);
        }

        return stack;
    };

    const updateNotificationBadge = () => {
        if (!notificationBadge) {
            return;
        }

        notificationBadge.textContent = String(unreadNotificationCount);
        notificationBadge.classList.toggle("is-hidden", unreadNotificationCount <= 0);
    };

    const buildNotificationUrl = (notification) => {
        if (notification?.relatedEntityType === "Booking" && notification.relatedEntityId) {
            return `/Payments/Checkout?bookingId=${encodeURIComponent(notification.relatedEntityId)}`;
        }

        if (notification?.relatedEntityType === "Contract" && notification.relatedEntityId) {
            return `/Contracts/Details/${encodeURIComponent(notification.relatedEntityId)}`;
        }

        return "/Account/Inbox";
    };

    const showNotificationToast = (notification) => {
        const stack = ensureToastStack();
        const toast = document.createElement("div");
        const link = buildNotificationUrl(notification);
        toast.className = "notification-toast";
        toast.innerHTML = `
            <strong>${escapeHtml(notification?.title || "Thông báo mới")}</strong>
            <p>${escapeHtml(notification?.message || "Bạn có một cập nhật mới từ ChillTour.")}</p>
            <a href="${link}">Xem chi tiết</a>
        `;

        stack.prepend(toast);
        window.setTimeout(() => toast.classList.add("is-visible"), 20);
        window.setTimeout(() => {
            toast.classList.remove("is-visible");
            window.setTimeout(() => toast.remove(), 220);
        }, 8000);
    };

    const appendNotificationToInbox = (notification) => {
        const list = document.querySelector(".customer-notification-list");
        if (!list) {
            return;
        }

        document.querySelector(".auth-alert--error")?.remove();

        const item = document.createElement("article");
        const link = buildNotificationUrl(notification);
        const isBooking = notification?.relatedEntityType === "Booking" && notification.relatedEntityId;
        const isContract = notification?.relatedEntityType === "Contract" && notification.relatedEntityId;
        const actionLabel = isBooking
            ? "Xem đơn liên quan"
            : isContract
                ? "Xem hợp đồng"
                : "Thông báo hệ thống";
        const actionHtml = isBooking || isContract
            ? `<a href="${link}" class="btn btn-outline-primary">${actionLabel}</a>`
            : `<span class="status-pill status-pill--info">${actionLabel}</span>`;

        item.className = "customer-notification-card customer-notification-card--wide is-unread";
        item.innerHTML = `
            <div class="customer-notification-card__main">
                <div class="customer-notification-card__head">
                    <div>
                        <span class="notification-dot is-unread"></span>
                        <h3>${escapeHtml(notification?.title || "Thông báo mới")}</h3>
                    </div>
                    <span>Vừa xong</span>
                </div>
                <p>${escapeHtml(notification?.message || "Bạn có một cập nhật mới từ ChillTour.")}</p>
            </div>
            <div class="customer-notification-card__actions">
                ${actionHtml}
            </div>
        `;
        list.prepend(item);
    };

    const startNotificationHub = async () => {
        if (!notificationLink || typeof signalR === "undefined") {
            return;
        }

        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/notifications")
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveNotification", (notification) => {
            unreadNotificationCount += 1;
            updateNotificationBadge();
            appendNotificationToInbox(notification);
            showNotificationToast(notification);
        });

        try {
            await connection.start();
        } catch {
            window.setTimeout(startNotificationHub, 5000);
        }
    };

    startNotificationHub();

    const floatingChat = document.querySelector("[data-floating-chat]");
    const floatingChatToggle = document.querySelector("[data-floating-chat-toggle]");
    const chatbotWidget = document.querySelector("[data-chatbot-widget]");
    const chatbotOpenButtons = document.querySelectorAll("[data-chatbot-open]");
    const chatbotCloseButton = document.querySelector("[data-chatbot-close]");
    const chatbotForm = document.querySelector("[data-chatbot-form]");
    const chatbotInput = document.querySelector("[data-chatbot-input]");
    const chatbotMessages = document.querySelector("[data-chatbot-messages]");

    const appendChatbotMessage = (text, type) => {
        if (!chatbotMessages) {
            return null;
        }

        const message = document.createElement("div");
        message.className = `chatbot-message chatbot-message--${type}`;
        message.textContent = text;
        chatbotMessages.appendChild(message);
        chatbotMessages.scrollTop = chatbotMessages.scrollHeight;
        return message;
    };

    const openChatbot = () => {
        chatbotWidget?.classList.add("is-open");
        chatbotWidget?.setAttribute("aria-hidden", "false");
        floatingChat?.classList.remove("is-open");
        floatingChatToggle?.setAttribute("aria-expanded", "false");
        window.setTimeout(() => chatbotInput?.focus(), 80);
    };

    const closeChatbot = () => {
        chatbotWidget?.classList.remove("is-open");
        chatbotWidget?.setAttribute("aria-hidden", "true");
    };

    floatingChatToggle?.addEventListener("click", () => {
        const isOpen = floatingChat?.classList.toggle("is-open") ?? false;
        floatingChatToggle.setAttribute("aria-expanded", String(isOpen));
    });

    chatbotOpenButtons.forEach((button) => {
        button.addEventListener("click", openChatbot);
    });

    chatbotCloseButton?.addEventListener("click", closeChatbot);

    chatbotForm?.addEventListener("submit", async (event) => {
        event.preventDefault();

        const text = chatbotInput?.value.trim() || "";
        if (!text) {
            return;
        }

        appendChatbotMessage(text, "user");
        if (chatbotInput) {
            chatbotInput.value = "";
            chatbotInput.disabled = true;
        }

        const loadingMessage = appendChatbotMessage("Đang kiểm tra dữ liệu tour...", "loading");

        try {
            const response = await fetch("/Chatbot/Ask", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({ message: text })
            });

            const result = await response.json();
            loadingMessage?.remove();
            appendChatbotMessage(result.message || "Tôi chưa có câu trả lời phù hợp. Bạn vui lòng thử lại.", "bot");
        } catch {
            loadingMessage?.remove();
            appendChatbotMessage("Hiện chưa kết nối được chatbot. Bạn vui lòng thử lại sau hoặc liên hệ Zalo.", "bot");
        } finally {
            if (chatbotInput) {
                chatbotInput.disabled = false;
                chatbotInput.focus();
            }
        }
    });
});

function escapeHtml(value) {
    const element = document.createElement("div");
    element.textContent = value;
    return element.innerHTML;
}
