import { http } from "boot/axios";

export default {
  getAllTimeInTimeOut (model) {
    return http.post("/time-in-time-out/list", model).then(response => response.data);
  },
  getTimeInTimeOut (id) {
    return http.get(`/time-in-time-out/${id}`).then(response => response.data);
  },
  getTimeInTimeOutDetails (id) {
    return http.get(`/time-in-time-out/details/${id}`).then(response => response.data);
  },
  getTimeInTimeOutDetailsByEmployeeId (employeeId) {
    return http.get(`/time-in-time-out/${employeeId}`).then(response => response.data);
  },
  getMovementRegByEmployeeId (employeeId) {
    return http.get(`/movementRegister/latest-record/${employeeId}`).then(response => response.data);
  },
  saveTimeInTimeOut (id) {
    if (id) {
      return http.put(`/time-in-time-out/${id}`).then(response => response.data);
    } else {
      return http.post("/time-in-time-out").then(response => response.data);
    }
  },
  // The dashboard's time clock. Each call answers with the signed-in employee's fresh status.
  // `silent` is for the background re-check, whose failure is not worth a toast.
  getMyTimeClockStatus (silent = false) {
    return http.get("/time-in-time-out/my/status", { silent }).then(response => response.data);
  },
  timeIn () {
    return http.post("/time-in-time-out").then(response => response.data);
  },
  // addLunch / addDinner: the employee confirmed, at the time-out, a meal break that was not logged.
  // note: an optional message saved with the session.
  timeOut (id, { addLunch = false, addDinner = false, note = "" } = {}) {
    return http.put(`/time-in-time-out/${id}`, { addLunch, addDinner, note }).then(response => response.data);
  },
  startBreak (id, breakReason) {
    return http.post(`/time-in-time-out/${id}/break/start`, { breakReason }).then(response => response.data);
  },
  // A break taken but not clocked: { startTime: "13:00", endTime: "13:45", breakReason } in site time.
  addPastBreak (id, model) {
    return http.post(`/time-in-time-out/${id}/break/add`, model).then(response => response.data);
  },
  // Gate pass: logged for the minutes chosen, { minutes, purpose }; changed the same way.
  logGatePass (id, model) {
    return http.post(`/time-in-time-out/${id}/gate-pass`, model).then(response => response.data);
  },
  updateGatePass (id, breakId, model) {
    return http.put(`/time-in-time-out/${id}/gate-pass/${breakId}`, model).then(response => response.data);
  },
  // Cancels a logged lunch, dinner or gate pass (the entries without a timer), by its id.
  cancelLoggedBreak (id, breakId, cancelReason) {
    return http.put(`/time-in-time-out/${id}/break/${breakId}/cancel`, { cancelReason }).then(response => response.data);
  },
  cancelBreak (id, cancelReason) {
    return http.put(`/time-in-time-out/${id}/break/cancel`, { cancelReason }).then(response => response.data);
  },
  endBreak (id) {
    return http.put(`/time-in-time-out/${id}/break/end`).then(response => response.data);
  },
  deleteTimeInTimeOut (id) {
    return http.delete(`/time-in-time-out/${id}`).then(response => response.data);
  }
};
