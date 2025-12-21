
function showDetails(bookingId, ref) {
    const modalRef = document.getElementById('modalRef');
    [cite_start]if (modalRef) modalRef.innerText = ref;

    const list = document.getElementById('passengerList');
    if (list) {
        [cite_start]list.innerHTML = '<div class="text-center p-4"><div class="spinner-border text-primary" role="status"></div><p class="mt-2 mb-0">Loading manifest...</p></div>';[cite: 37]
    }

    const modalElement = document.getElementById('detailsModal');
    if (modalElement) {
        const myModal = new bootstrap.Modal(modalElement);
        [cite_start]myModal.show();
    }

    [cite_start]fetch('/Booking/GetBookingDetails/' + bookingId)
        .then(response => {
        if (!response.ok) throw new Error('Failed to load passenger data');
        return response.json();
    })
        .then(data => {
            if (!list) return;
            let html = '';
            if (data.length === 0) {
                html = '<div class="text-center p-3 text-muted">No passenger records found.</div>';
            } else {
                data.forEach(p => {
                    html += `
                        <div class="list-group-item d-flex justify-content-between align-items-center border-0 px-0 py-3 border-bottom shadow-none">
                            <div>
                                <div class="fw-bold text-dark">${p.name}</div>
                                <small class="text-muted text-uppercase">${p.ticketType}</small>
                            </div>
                            <span class="badge bg-primary rounded-pill px-3 py-2">Seat: ${p.seatNumber}</span>
                        </div>`;
                });
            }
            list.innerHTML = html;
        })
        .catch(error => {
            if (list) list.innerHTML = `<div class="alert alert-danger m-3 small">${error.message}</div>`;
        });
}


function handleReject(bookingId) {

    let reason = prompt("Please enter the reason for rejection:");


    if (reason === null) return;


    if (reason.trim() === "") {
       alert("You must provide a reason to reject the refund.");
        return;
    }

    const reasonInput = document.getElementById('reason-' + bookingId);
    const form = document.getElementById('rejectForm-' + bookingId);

    if (reasonInput && form) {
        reasonInput.value = reason;
        form.submit();
    }
}