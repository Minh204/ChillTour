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
