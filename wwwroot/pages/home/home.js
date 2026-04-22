document.addEventListener("DOMContentLoaded", function () {
    const scrollRails = document.querySelectorAll("[data-scroll-rail]");

    scrollRails.forEach((rail) => {
        const section = rail.closest("section");
        const leftButton = section?.querySelector("[data-scroll-left]");
        const rightButton = section?.querySelector("[data-scroll-right]");
        const track = rail.querySelector(".travel-home__rail-track");
        const railItems = track ? Array.from(track.children) : [];

        if (!leftButton || !rightButton) {
            return;
        }

        const updateButtons = () => {
            const maxScrollLeft = rail.scrollWidth - rail.clientWidth - 4;
            leftButton.disabled = rail.scrollLeft <= 4;
            rightButton.disabled = rail.scrollLeft >= maxScrollLeft;
        };

        const getScrollStep = () => {
            if (railItems.length > 0) {
                const item = railItems[0];
                const itemStyles = window.getComputedStyle(item);
                const marginRight = Number.parseFloat(itemStyles.marginRight || "0");
                return item.getBoundingClientRect().width + marginRight + 18;
            }

            return Math.max(rail.clientWidth * 0.82, 280);
        };

        const scrollByAmount = (direction) => {
            rail.scrollBy({
                left: direction * getScrollStep(),
                behavior: "smooth"
            });
        };

        leftButton.addEventListener("click", () => scrollByAmount(-1));
        rightButton.addEventListener("click", () => scrollByAmount(1));
        rail.addEventListener("scroll", updateButtons, { passive: true });
        window.addEventListener("resize", updateButtons);
        updateButtons();
    });
});
