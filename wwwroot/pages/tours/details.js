document.addEventListener("DOMContentLoaded", function () {
    const formatCurrency = (value) => {
        const number = Number(value);
        if (Number.isNaN(number)) {
            return "Liên hệ";
        }

        return `${number.toLocaleString("vi-VN")} đ`;
    };

    const scheduleSelect = document.getElementById("BookingForm_TourScheduleId");
    const updateScheduleDisplay = () => {
        if (!scheduleSelect) {
            return;
        }

        const selectedOption = scheduleSelect.options[scheduleSelect.selectedIndex];
        if (!selectedOption) {
            return;
        }

        const adultPrice = selectedOption.dataset.adultPrice;
        const singleSupplement = selectedOption.dataset.singleSupplement;
        const departureDate = selectedOption.dataset.departureDate;
        const seatStatus = selectedOption.dataset.seatStatus;

        document.querySelectorAll("[data-schedule-adult-price]").forEach((node) => {
            node.textContent = formatCurrency(adultPrice);
        });

        document.querySelectorAll("[data-schedule-departure]").forEach((node) => {
            node.textContent = departureDate || "Liên hệ";
        });

        document.querySelectorAll("[data-schedule-seat-status]").forEach((node) => {
            node.textContent = seatStatus || "Đang cập nhật";
        });

        const supplementNote = document.querySelector(".tour-booking-card__note--supplement");
        if (supplementNote) {
            supplementNote.textContent = singleSupplement && Number(singleSupplement) > 0
                ? `Phụ thu phòng đơn: ${formatCurrency(singleSupplement)} / phòng.`
                : "Tour này hiện chưa cấu hình phụ thu phòng đơn.";
        }
    };

    if (scheduleSelect) {
        scheduleSelect.addEventListener("change", updateScheduleDisplay);
        updateScheduleDisplay();
    }

    const highlightContent = document.getElementById("tourHighlightContent");
    const highlightToggle = document.getElementById("tourHighlightToggle");
    if (highlightContent && highlightToggle) {
        highlightToggle.addEventListener("click", () => {
            const isCollapsed = highlightContent.classList.toggle("is-collapsed");
            highlightToggle.textContent = isCollapsed ? "Xem thêm" : "Thu gọn";
            highlightToggle.setAttribute("aria-expanded", (!isCollapsed).ToString().toLowerCase());
        });
    }

    const imageModalElement = document.getElementById("tourImageModal");
    const imagePreview = document.getElementById("tourImageModalPreview");
    if (imageModalElement && imagePreview && window.bootstrap) {
        const imageModal = new bootstrap.Modal(imageModalElement);
        document.querySelectorAll(".tour-program-image-trigger").forEach((button) => {
            button.addEventListener("click", () => {
                imagePreview.setAttribute("src", button.dataset.imageSrc || "");
                imagePreview.setAttribute("alt", button.dataset.imageAlt || "");
                imageModal.show();
            });
        });
    }
});
