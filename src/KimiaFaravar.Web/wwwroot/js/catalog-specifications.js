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
if (window.location.hash === "#groups") {
    const groupTab = document.querySelector('[data-bs-target="#groups"]');
    if (groupTab) bootstrap.Tab.getOrCreateInstance(groupTab).show();
}
