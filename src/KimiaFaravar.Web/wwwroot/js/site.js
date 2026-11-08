// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(() => {
    const clock = document.getElementById("live-client-clock");
    if (!clock) return;
    const formatter = new Intl.DateTimeFormat("fa-IR", {
        hour: "2-digit", minute: "2-digit", second: "2-digit", hour12: false
    });
    const update = () => { clock.textContent = formatter.format(new Date()); };
    update();
    window.setInterval(update, 1000);
})();

(() => {
    document.querySelectorAll(".js-reset-password").forEach(button => {
        button.addEventListener("click", () => {
            document.getElementById("reset-password-user-id").value = button.dataset.userId;
            document.getElementById("reset-password-user-name").textContent = button.dataset.userName;
            document.getElementById("admin-new-password").value = "";
        });
    });

    document.querySelectorAll(".js-user-status").forEach(button => {
        button.addEventListener("click", () => {
            const disabling = button.dataset.mode === "disable";
            const form = document.getElementById("user-status-form");
            form.action = button.dataset.action;
            document.getElementById("status-user-id").value = button.dataset.userId;
            document.getElementById("userStatusTitle").textContent = disabling ? "غیرفعال‌سازی کاربر" : "فعال‌سازی مجدد کاربر";
            document.getElementById("user-status-icon").textContent = disabling ? "⚠️" : "✅";
            document.getElementById("user-status-message").textContent = disabling
                ? `آیا «${button.dataset.userName}» غیرفعال شود؟`
                : `آیا حساب «${button.dataset.userName}» دوباره فعال شود؟`;
            document.getElementById("user-status-note").textContent = disabling
                ? "سوابق کاربر حذف نمی‌شود و نشست‌های فعال او بسته خواهند شد."
                : "کاربر دوباره امکان ورود به سامانه را خواهد داشت.";
            const submit = document.getElementById("user-status-submit");
            submit.textContent = disabling ? "غیرفعال‌سازی" : "فعال‌سازی";
            submit.className = disabling ? "btn btn-danger" : "btn btn-success";
        });
    });

    const ownForm = document.querySelector("#changeOwnPasswordModal form");
    if (ownForm) {
        ownForm.addEventListener("submit", event => {
            const password = document.getElementById("own-new-password").value;
            const confirmation = document.getElementById("own-confirm-password").value;
            const message = document.getElementById("own-password-mismatch");
            const matches = password === confirmation;
            message.classList.toggle("d-none", matches);
            if (!matches) event.preventDefault();
        });
    }
})();


(() => {
    document.querySelectorAll(".table").forEach(table => {
        const labels = Array.from(table.querySelectorAll("thead th")).map(header => header.textContent.trim());
        table.querySelectorAll("tbody tr").forEach(row => {
            Array.from(row.cells).forEach((cell, index) => {
                if (cell.colSpan === 1 && labels[index] && !cell.hasAttribute("data-label")) {
                    cell.setAttribute("data-label", labels[index]);
                }
            });
        });
    });
})();
