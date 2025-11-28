// Global variable to store the name we expect
let expectedName = "";

function openDeleteModal(name) {
    expectedName = name;

    // Reset the modal state
    const input = document.getElementById("deleteInput");
    const btn = document.getElementById("confirmDeleteBtn");

    input.value = "";
    btn.disabled = true;
    btn.classList.remove("active");

    // Show the modal
    document.getElementById("deleteModal").style.display = "flex";

    // Focus the input automatically
    input.focus();
}

function closeDeleteModal() {
    document.getElementById("deleteModal").style.display = "none";
}

// Add event listener to the input box
document.addEventListener("DOMContentLoaded", function () {
    const input = document.getElementById("deleteInput");
    const btn = document.getElementById("confirmDeleteBtn");

    if (input && btn) {
        input.addEventListener("input", function () {
            if (this.value === expectedName) {
                // Match! Enable button
                btn.disabled = false;
                btn.classList.add("active");
            } else {
                // No match
                btn.disabled = true;
                btn.classList.remove("active");
            }
        });
    }

    // Allow closing with Escape key
    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            closeDeleteModal();
        }
    });
});