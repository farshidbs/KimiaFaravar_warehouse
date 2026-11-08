(() => {
    const form = document.getElementById("stock-filter-form");
    const results = document.getElementById("stock-results");
    if (!form || !results) return;
    const error = document.getElementById("stock-filter-error");
    let pending;
    async function load(url, pushHistory = true) {
        pending?.abort();
        const request = new AbortController();
        pending = request;
        error.classList.add("d-none");
        results.setAttribute("aria-busy", "true");
        try {
            const response = await fetch(url, { headers: { "X-Stock-Partial": "true" }, signal: request.signal });
            if (response.redirected && new URL(response.url).pathname.toLowerCase().includes("/account/login")) {
                window.location.assign(response.url);
                return;
            }
            if (!response.ok) throw new Error("Request failed");
            const content = await response.text();
            if (request.signal.aborted) return;
            results.innerHTML = content;
            // Dynamic rows need the same mobile column labels as the initial table.
            results.querySelectorAll("table").forEach(table => {
                const labels = Array.from(table.querySelectorAll("thead th"), th => th.textContent.trim());
                table.querySelectorAll("tbody tr").forEach(row => Array.from(row.cells).forEach((cell, index) => {
                    if (cell.colSpan === 1) cell.dataset.label = labels[index] || "";
                }));
            });
            document.querySelectorAll("[data-stock-export]").forEach(link => {
                const target = new URL(link.href);
                target.search = url.search;
                target.searchParams.delete("page");
                link.href = target.href;
            });
            if (pushHistory) window.history.pushState(null, "", url);
        } catch (failure) {
            if (failure.name !== "AbortError") {
                error.textContent = "دریافت موجودی ناموفق بود. دوباره جست‌وجو کنید.";
                error.classList.remove("d-none");
            }
        } finally {
            if (pending === request) results.setAttribute("aria-busy", "false");
        }
    }
    form.addEventListener("submit", event => {
        event.preventDefault();
        const url = new URL(form.action);
        url.search = new URLSearchParams(new FormData(form)).toString();
        load(url);
    });
    document.getElementById("stock-group").addEventListener("change", () => form.requestSubmit());
    results.addEventListener("click", event => {
        const link = event.target.closest("nav a");
        if (!link || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        load(new URL(link.href));
    });
    document.getElementById("stock-clear").addEventListener("click", event => {
        event.preventDefault();
        form.elements.groupId.value = "";
        form.elements.search.value = "";
        load(new URL(form.action));
    });
    window.addEventListener("popstate", () => {
        const url = new URL(window.location.href);
        form.elements.groupId.value = url.searchParams.get("groupId") || "";
        form.elements.search.value = url.searchParams.get("search") || "";
        load(url, false);
    });
})();

document.querySelectorAll(".stock-movement-modal").forEach(modal => {
    modal.addEventListener("show.bs.modal", event => {
        const button = event.relatedTarget;
        if (!button) return;
        const form = modal.querySelector("form");
        form.reset();
        const filters = new URL(window.location.href).searchParams;
        ["groupId", "search", "page"].forEach(name => {
            form.elements[name].value = filters.get(name) || (name === "page" ? "1" : "");
        });
        form.querySelector('[name="productId"]').value = button.dataset.productId;
        modal.querySelector("[data-product-summary]").textContent = `${button.dataset.productName} — ${button.dataset.productSku}`;
        modal.querySelector("[data-stock-summary]").textContent = `موجودی فعلی: ${button.dataset.productStock}`;
        form.querySelector('[type="submit"]').disabled = false;
    });
    modal.addEventListener("shown.bs.modal", () => modal.querySelector('[name="quantity"]').focus());
    modal.querySelector("form").addEventListener("submit", event => {
        if (event.target.checkValidity()) event.target.querySelector('[type="submit"]').disabled = true;
    });
});
