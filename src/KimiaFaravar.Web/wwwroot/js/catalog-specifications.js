(function ($) {
    "use strict";
    const $group = $("#product-group");
    const $container = $("#dynamic-specifications");
    const $form = $("#product-form");
    const $error = $("#product-form-error");
    let specs = [];
    let requestVersion = 0;

    function filterGroupProducts() {
        const groupId = String($group.val() || "");
        const rows = document.querySelectorAll("#group-products [data-product-group]");
        let count = 0;
        rows.forEach(row => {
            const matches = groupId !== "" && row.dataset.productGroup === groupId;
            row.classList.toggle("d-none", !matches);
            if (matches) count++;
        });
        $("#group-products .table-responsive").toggleClass("d-none", count === 0);
        $("#group-products-status").text(!groupId
            ? "برای نمایش کالاهای موجود، گروه کالا را انتخاب کنید."
            : count === 0 ? "هنوز کالایی در این گروه ثبت نشده است." : `${count} کالا در گروه انتخاب‌شده`);
    }
    filterGroupProducts();

    function showError(message) { $error.text(message).removeClass("d-none"); }
    function hideError() { $error.addClass("d-none").empty(); }
    function render(specifications) {
        $container.empty();
        if (!specifications.length) {
            $container.append($("<p>").addClass("text-muted").text("برای این گروه مشخصه‌ای تعریف نشده است."));
            return;
        }
        specifications.forEach(function (s) {
            const $field = $("<div>").addClass("col-md-6");
            const id = "spec-" + s.id;
            const $label = $("<label>").addClass("form-label").attr("for", id).text(s.name + (s.isRequired ? " *" : ""));
            let $input;
            if (s.dataType === 4 || s.dataType === 3) {
                $input = $("<select>").append($("<option>").val("").text("انتخاب کنید..."));
                if (s.dataType === 3) {
                    $input.append($("<option>").val("true").text("بله"), $("<option>").val("false").text("خیر"));
                } else {
                    s.options.forEach(function (o) { $input.append($("<option>").val(o.id).text(o.value)); });
                }
                $input.addClass("form-select");
            } else {
                $input = $("<input>").addClass("form-control");
                if (s.dataType === 1 || s.dataType === 2) {
                    $input.attr({ type: "number", step: s.dataType === 1 ? "any" : "1" }).addClass("text-start").css("direction", "ltr");
                } else { $input.attr("type", "text").attr("maxlength", "300"); }
            }
            $input.attr("id", id).attr("data-spec-id", s.id).prop("required", s.isRequired);
            $field.append($label, $input);
            $container.append($field);
        });
    }
    $group.on("change", function () {
        filterGroupProducts();
        hideError();
        specs = [];
        const version = ++requestVersion;
        const groupId = $group.val();
        $container.empty().append($("<p>").addClass("text-muted").text("در حال دریافت مشخصات..."));
        if (!groupId) { $container.text("گروه کالا را انتخاب کنید."); return; }
        $.getJSON("/Catalog/EffectiveSpecifications", { groupId: groupId })
            .done(function (data) { if (version === requestVersion) { specs = data; render(specs); } })
            .fail(function (xhr) { if (version === requestVersion) { $container.empty(); showError(xhr.responseJSON?.error || "دریافت مشخصات گروه ناموفق بود."); } });
    });
    $form.on("submit", function (event) {
        hideError();
        if (!$group.val()) { event.preventDefault(); showError("گروه کالا را انتخاب کنید."); return; }
        if (specs.length !== $container.find("[data-spec-id]").length) {
            event.preventDefault(); showError("ابتدا منتظر دریافت کامل مشخصات گروه بمانید."); return;
        }
        const values = {};
        $container.find("[data-spec-id]").each(function () {
            const val = $(this).val();
            if (val !== null && String(val).trim() !== "") values[$(this).attr("data-spec-id")] = String(val).trim();
        });
        $("#product-values-json").val(JSON.stringify(values));
    });
})(jQuery);

document.getElementById("groupVisibilityModal")?.addEventListener("show.bs.modal", event => {
    const button = event.relatedTarget;
    if (!button) return;
    document.getElementById("groupVisibilityId").value = button.dataset.groupId;
    document.getElementById("groupVisibilityName").textContent = button.dataset.groupName;
    document.getElementById("groupVisibilityInternal").checked = button.dataset.internalSales === "true";
    document.getElementById("groupVisibilityMarket").checked = button.dataset.marketSales === "true";
});
// Keep the group editor visible after saving its visibility settings.
if (["#groups", "#new-option", "#new-spec", "#catalog-view"].includes(window.location.hash)) {
    const groupTab = document.querySelector(`[data-bs-target="${window.location.hash}"]`);
    if (groupTab) bootstrap.Tab.getOrCreateInstance(groupTab).show();
}

document.getElementById("productVisibilityModal")?.addEventListener("show.bs.modal", event => {
    const button = event.relatedTarget;
    if (!button) return;
    document.getElementById("productVisibilityId").value = button.dataset.productId;
    document.getElementById("productVisibilityName").textContent = button.dataset.productName;
    document.getElementById("productVisibilityInternal").checked = button.dataset.internalSales === "true";
    document.getElementById("productVisibilityMarket").checked = button.dataset.marketSales === "true";
});

(() => {
    const group = document.getElementById("specification-group");
    if (!group) return;
    const rows = document.getElementById("group-specifications-rows");
    const table = document.getElementById("group-specifications-table");
    const status = document.getElementById("group-specifications-status");
    const labels = ["مشخصه", "نوع داده", "گزینه‌ها", "اجباری", "کلید یکتایی", "گروه تعریف‌کننده"];
    const types = ["متن", "عدد اعشاری", "عدد صحیح", "بله / خیر", "لیست انتخابی"];
    let pending;
    async function load() {
        pending?.abort();
        const request = new AbortController();
        pending = request;
        rows.replaceChildren();
        table.classList.add("d-none");
        status.className = "text-muted";
        if (!group.value) { status.textContent = "گروه را برای مشاهده مشخصات انتخاب کنید."; return; }
        status.textContent = "در حال دریافت مشخصات گروه...";
        try {
            const response = await fetch(`/Catalog/GroupSpecifications?groupId=${encodeURIComponent(group.value)}`, { signal: request.signal });
            if (response.redirected) { window.location.assign(response.url); return; }
            const data = await response.json();
            if (!response.ok) throw new Error(data.error || "دریافت مشخصات ناموفق بود.");
            if (request.signal.aborted) return;
            data.forEach(spec => {
                const row = document.createElement("tr");
                const values = [spec.name, types[spec.dataType] || "—",
                    spec.dataType === 4 ? spec.options.map(option => option.value).join("، ") || "بدون گزینه" : "—",
                    spec.isRequired ? "بله" : "خیر", spec.isVariantKey ? "بله" : "خیر",
                    `${spec.groupName || "—"}${spec.inherited ? " (ارث‌رسیده)" : ""}`];
                values.forEach((value, index) => {
                    const cell = document.createElement("td");
                    cell.textContent = value;
                    cell.dataset.label = labels[index];
                    row.append(cell);
                });
                rows.append(row);
            });
            table.classList.toggle("d-none", data.length === 0);
            status.textContent = data.length ? `${data.length} مشخصه برای این گروه` : "برای این گروه مشخصه‌ای تعریف نشده است.";
        } catch (error) {
            if (error.name === "AbortError") return;
            status.className = "text-danger";
            status.textContent = "دریافت مشخصات ناموفق بود؛ گروه را دوباره انتخاب کنید.";
        }
    }
    group.addEventListener("change", load);
    document.querySelector('[data-bs-target="#new-spec"]')?.addEventListener("shown.bs.tab", load);
    load();
})();

document.getElementById("specification-option-form")?.addEventListener("submit", async event => {
    event.preventDefault();
    const form = event.currentTarget;
    if (form.dataset.saving === "true") return;
    const submit = form.querySelector("button");
    const feedback = document.getElementById("option-feedback");
    form.dataset.saving = "true";
    submit.disabled = true;
    form.setAttribute("aria-busy", "true");
    feedback.className = "alert d-none";
    try {
        const response = await fetch(form.action, {
            method: "POST", body: new FormData(form), headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (response.redirected) { window.location.assign(response.url); return; }
        const data = await response.json();
        if (!response.ok) throw new Error(data.error || "ثبت گزینه ناموفق بود.");
        const cell = document.querySelector(`[data-options-for="${data.specificationId}"]`);
        if (cell) {
            cell.querySelector("[data-options-empty]")?.remove();
            const badge = document.createElement("span");
            badge.className = "badge bg-light text-dark border me-1";
            badge.dataset.optionId = data.id;
            badge.textContent = data.value;
            cell.append(badge);
        }
        form.elements.value.value = "";
        feedback.textContent = "گزینه جدید ثبت شد و به فهرست اضافه شد.";
        feedback.className = "alert alert-success";
        form.elements.value.focus();
    } catch (error) {
        feedback.textContent = error.message === "Failed to fetch" ? "ارتباط برقرار نشد؛ دوباره تلاش کنید." : error.message;
        feedback.className = "alert alert-danger";
    } finally {
        submit.disabled = false;
        form.dataset.saving = "false";
        form.setAttribute("aria-busy", "false");
    }
});
