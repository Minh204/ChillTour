document.addEventListener("DOMContentLoaded", function () {
    const lineCharts = document.querySelectorAll("[data-line-chart]");

    lineCharts.forEach((chart) => {
        const line = chart.querySelector(".admin-line-chart__line");
        const area = chart.querySelector(".admin-line-chart__area");
        const points = chart.querySelectorAll(".admin-line-chart__point");

        if (!line) {
            return;
        }

        const length = line.getTotalLength();
        line.style.strokeDasharray = `${length}`;
        line.style.strokeDashoffset = `${length}`;

        if (area) {
            area.style.opacity = "0";
        }

        points.forEach((point, index) => {
            point.style.opacity = "0";
            point.style.transform = "scale(0.75)";
            point.style.transformOrigin = "center";
            point.style.transitionDelay = `${0.35 + (index * 0.06)}s`;
        });

        requestAnimationFrame(() => {
            line.classList.add("is-animated");

            if (area) {
                area.classList.add("is-animated");
            }

            points.forEach((point) => point.classList.add("is-animated"));
        });
    });
});
