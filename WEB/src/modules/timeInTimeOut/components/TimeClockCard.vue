<template>
  <q-card flat bordered class="time-clock">
    <q-card-section class="row items-center q-py-sm time-clock__head">
      <q-icon name="o_schedule" color="primary" size="20px" class="q-mr-xs" />
      <div class="text-subtitle1 text-weight-medium text-primary ellipsis">Time In - Out</div>
      <q-badge
        v-if="!loading"
        :color="stateBadge.color"
        :text-color="stateBadge.textColor"
        class="time-clock__state q-ml-sm"
      >
        <q-icon :name="stateBadge.icon" size="13px" class="q-mr-xs" />
        {{ stateBadge.label }}
        <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">{{ stateBadge.tip }}</q-tooltip>
      </q-badge>
      <q-space />
      <q-btn
        v-if="!loading && pendingTimesheets.length"
        flat
        dense
        no-caps
        size="sm"
        icon="o_event_note"
        class="time-clock__reminder q-mr-sm"
        :label="timesheetReminderLabel"
      >
        <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">
          Reminder: {{ timesheetReminderText }} Click to see the days. This is only a reminder; you can carry on
          working.
        </q-tooltip>
        <q-menu anchor="bottom right" self="top right">
          <q-list dense style="min-width: 280px; max-width: 340px">
            <q-item-label header class="q-py-sm">Timesheets not filled</q-item-label>
              <q-item v-for="day in pendingTimesheets" :key="day.dateText">
                <q-item-section avatar style="min-width: 44px" class="text-weight-medium">{{ day.dayText }}</q-item-section>
                <q-item-section>{{ day.dateText }}</q-item-section>
              </q-item>
            <q-item>
              <q-item-section class="time-clock__hint">
                A timesheet can be filled for this week and last week.
              </q-item-section>
            </q-item>
            <q-separator />
            <q-item v-close-popup clickable @click="openTimesheet">
              <q-item-section avatar style="min-width: 44px"><q-icon name="o_open_in_new" color="primary" size="20px" /></q-item-section>
              <q-item-section class="text-primary text-weight-medium">Open Timesheet</q-item-section>
            </q-item>
          </q-list>
        </q-menu>
      </q-btn>
      <q-btn
        flat
        dense
        no-caps
        size="sm"
        color="primary"
        icon="o_open_in_new"
        label="View All"
        @click="$router.push('/time-in-time-out/list')"
      >
        <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">See all your Time In - Out entries, day by day.</q-tooltip>
      </q-btn>
    </q-card-section>
    <q-separator />

    <q-card-section v-if="loading">
      <q-skeleton type="text" width="40%" />
      <q-skeleton type="rect" height="48px" class="q-mt-sm" />
    </q-card-section>

    <q-card-section v-else class="q-pt-md">
      <q-banner v-if="!status.hasEmployee" dense rounded class="bg-blue-1 text-primary q-mb-md">
        <template #avatar><q-icon name="o_info" color="primary" /></template>
        Your login is not linked to an employee record, so time cannot be recorded. Ask HR to set up your employee.
      </q-banner>

      <q-banner v-if="status.isOverdue" dense rounded class="time-clock__warn q-mb-md">
        <template #avatar><q-icon name="o_warning" /></template>
        You have been timed in since {{ status.timeInDateText }} {{ status.timeInText }}, so the time-out looks
        missed. Time out to close this session, then time in again. A session counts at most 24 hours.
      </q-banner>

      <div class="time-clock__body">
        <!-- The answer the card exists for: time worked, breaks already taken out. -->
        <div class="time-clock__hero">
          <div class="record-label">Working hours</div>
          <div class="time-clock__timer" role="timer" :aria-label="`Working hours ${clock(live.worked)}`">
            {{ clock(live.worked) }}
          </div>
          <div class="time-clock__hint">{{ heroHint }}</div>
          <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">
            The time you have actually worked: Total minus breaks and gate pass. This is what is saved as your working hours
            when you time out.
          </q-tooltip>
        </div>

        <div class="time-clock__stats">
          <div class="time-clock__stat">
            <div class="record-label">Time in</div>
            <div class="time-clock__value">{{ status.timeInText || "—" }}</div>
            <div v-if="showTimeInDate" class="time-clock__hint">{{ status.timeInDateText }}</div>
            <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">When you started today. It is set when you click Time In.</q-tooltip>
          </div>
          <div class="time-clock__stat">
            <div class="record-label">Time out</div>
            <div class="time-clock__value">{{ status.timeOutText || "—" }}</div>
            <div v-if="showTimeOutDate" class="time-clock__hint">{{ status.timeOutDateText }}</div>
            <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">When you finished. It is set when you click Time Out.</q-tooltip>
          </div>
          <div v-if="!breaksOff" class="time-clock__stat">
            <div class="record-label">Break</div>
            <div :class="['time-clock__value', { 'time-clock__value--break': breakOver > 0 }]">{{ clock(liveBreakUsed) }}</div>
            <div class="time-clock__hint">{{ breakHint }}</div>
            <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">
              All your breaks today in one figure, lunch or dinner included ({{ breakCountText.toLowerCase() }}).
              The standard is {{ minutes(breakStandard) }} mins a day, of which lunch or dinner is
              {{ minutes(lunchAllowance) }} mins. You may take more when you need to; the extra is shown here and
              comes off your working hours like any break. Gate pass is counted separately.
            </q-tooltip>
          </div>
          <div v-if="showGatePass && !breaksOff" class="time-clock__stat">
            <div class="record-label">
              Gate pass
              <q-icon name="o_info" size="13px" class="q-ml-xs time-clock__help" />
            </div>
            <div :class="['time-clock__value', { 'time-clock__value--break': gatePassLeft <= 0 }]">{{ clock(liveGatePass) }}</div>
            <div class="time-clock__hint">{{ gatePassHint }}</div>
            <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">
              Time out of the office on a gate pass today. You may take up to {{ duration(gatePassMax) }} a day, in
              one go or several. It comes off your working hours and is counted apart from your breaks.
            </q-tooltip>
          </div>
          <div class="time-clock__stat">
            <div class="record-label">
              Total
              <q-icon name="o_info" size="13px" class="q-ml-xs time-clock__help" />
            </div>
            <div class="time-clock__value">{{ clock(live.gross) }}</div>
            <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">
              From your time in to {{ isCompleted ? "your time out" : "now" }}, breaks included.
              Working hours = Total − Break − Gate pass.
            </q-tooltip>
          </div>
        </div>

        <div class="time-clock__actions">
          <template v-if="isNotStarted || isCompleted">
            <q-btn
              :unelevated="isNotStarted"
              :outline="isCompleted"
              no-caps
              color="primary"
              icon="o_login"
              :label="isCompleted ? 'Time In Again' : 'Time In'"
              :loading="busy === 'in'"
              :disable="!!busy || !status.hasEmployee"
              @click="onTimeIn"
            >
              <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">
                {{ isCompleted ? "Back at work today? This starts another session from now." : "Start your day. Your working hours count from now." }}
              </q-tooltip>
            </q-btn>
          </template>

          <template v-else>
            <q-btn
              v-if="isOnBreak && !breaksOff"
              unelevated
              no-caps
              color="primary"
              icon="o_play_arrow"
              :label="`End ${awayName}`"
              :loading="busy === 'breakEnd'"
              :disable="!!busy"
              @click="onEndBreak"
            >
              <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">Back at work? This ends the break and your working hours count again.</q-tooltip>
            </q-btn>
            <!-- Wrapped so the tooltip still shows while the button is disabled, to say why. -->
            <div v-else-if="!breaksOff">
              <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">
                {{ status.isOverdue
                  ? "This session was left open too long. Time out first, then time in again."
                  : "Stepping away? Pick a reason and your working hours pause until you click End Break." }}
              </q-tooltip>
              <q-btn-dropdown
              ref="breakMenu"
              outline
              no-caps
              color="primary"
              icon="o_free_breakfast"
              label="Start Break"
              :loading="busy === 'breakStart'"
              :disable="!!busy || status.isOverdue"
              @show="otherReason = ''"
            >
              <q-list dense style="min-width: 240px">
                <q-item-label header class="q-py-sm">Reason for the break</q-item-label>
                <q-item
                  v-for="reason in reasonOptions"
                  :key="reason"
                  v-close-popup="!isReasonBlocked(reason)"
                  clickable
                  :disable="isReasonBlocked(reason)"
                  @click="onStartBreak(reason)"
                >
                  <q-item-section>{{ reason }}</q-item-section>
                  <q-item-section v-if="reasonCaption(reason)" side class="time-clock__hint">
                    {{ reasonCaption(reason) }}
                  </q-item-section>
                </q-item>
                <q-separator />
                <q-item>
                  <q-item-section>
                    <q-input
                      v-model="otherReason"
                      outlined
                      dense
                      maxlength="100"
                      placeholder="Other reason"
                      aria-label="Other reason for the break"
                      @keyup.enter="onStartOtherBreak"
                    >
                      <template #append>
                        <q-btn flat round dense color="primary" icon="o_send" :disable="!otherReason.trim()" @click="onStartOtherBreak">
                          <q-tooltip>Start break</q-tooltip>
                        </q-btn>
                      </template>
                    </q-input>
                  </q-item-section>
                </q-item>
              </q-list>
              </q-btn-dropdown>
            </div>
            <q-btn
              v-if="isOnBreak && !breaksOff"
              outline
              no-caps
              color="grey-8"
              icon="o_cancel"
              :label="`Cancel ${awayName}`"
              :loading="busy === 'breakCancel'"
              :disable="!!busy"
              @click="onCancelBreak"
            >
              <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">
                Started this break by mistake? Cancel it with a reason. It stays in the list as cancelled, counts
                0 mins, and the time since it started counts as working hours.
              </q-tooltip>
            </q-btn>
            <q-btn
              :unelevated="!isOnBreak"
              :outline="isOnBreak"
              no-caps
              color="primary"
              icon="o_logout"
              label="Time Out"
              :loading="busy === 'out'"
              :disable="!!busy"
              @click="onTimeOut"
            >
              <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">
                Finish for the day. Your total, break and working hours are saved{{ isOnBreak ? ", and the running break ends now" : "" }}.
                {{ mealsNotLogged.length ? `You will be asked whether you took ${mealsNotLogged.join(" or ").toLowerCase()}.` : "" }}
              </q-tooltip>
            </q-btn>
          </template>
        </div>
      </div>

      <div v-if="status.timeOutNote" class="time-clock__note q-mt-md">
        <q-icon name="o_sticky_note_2" size="16px" color="primary" />
        <span>{{ status.timeOutNote }}</span>
        <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">The note you added when you timed out.</q-tooltip>
      </div>

      <div v-if="status.sessionId && !breaksOff" class="row items-center q-mt-md">
        <div class="record-label q-mb-none">Breaks</div>
        <q-space />
        <q-btn
          flat
          dense
          no-caps
          size="sm"
          color="primary"
          v-if="canLogAmounts && !mealLogged"
          icon="o_restaurant"
          label="Add Lunch / Dinner Break"
          :disable="!!busy"
        >
          <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">
            Adds your lunch or dinner break as {{ minutes(lunchAllowance) }} mins straight away. There is no timer
            to start or end. One lunch or dinner break a day; you can cancel it from the list below.
          </q-tooltip>
          <q-menu anchor="bottom right" self="top right">
            <q-list dense style="min-width: 200px">
              <q-item-label header class="q-py-sm">Add {{ minutes(lunchAllowance) }} mins for</q-item-label>
              <q-item v-close-popup clickable @click="onStartBreak('Lunch')">
                <q-item-section avatar style="min-width: 36px"><q-icon name="o_restaurant" size="20px" /></q-item-section>
                <q-item-section>Lunch</q-item-section>
              </q-item>
              <q-item v-close-popup clickable @click="onStartBreak('Dinner')">
                <q-item-section avatar style="min-width: 36px"><q-icon name="o_dinner_dining" size="20px" /></q-item-section>
                <q-item-section>Dinner</q-item-section>
              </q-item>
            </q-list>
          </q-menu>
        </q-btn>
        <q-btn
          v-if="canLogAmounts && gatePassMinutesLeft > 0"
          flat
          dense
          no-caps
          size="sm"
          color="primary"
          icon="o_directions_walk"
          label="Add Gate Pass"
          :disable="!!busy"
          @click="openGatePass()"
        >
          <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">
            Going out on a gate pass? Choose how many minutes on the slider and it is logged straight away. You
            have {{ duration(gatePassLeft) }} of {{ duration(gatePassMax) }} left today, and you can change or
            cancel it afterwards.
          </q-tooltip>
        </q-btn>
        <q-btn
          flat
          dense
          no-caps
          size="sm"
          color="primary"
          icon="o_more_time"
          v-if="!status.isOverdue && !breaksOff"
          label="Add Missed Break"
          :disable="!!busy"
          @click="openPastBreak(false)"
        >
          <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">
            Took a break (tea, personal) and forgot to start it here? Add it with its Break Out and Break In
            times. Lunch, dinner and gate pass have their own buttons.
          </q-tooltip>
        </q-btn>
      </div>
      <div v-if="status.sessionId && !status.breaks.length && !breaksOff" class="time-clock__hint">
        No breaks yet. Use Start Break when you step away, or add one you already took.
      </div>

      <div v-if="status.breaks.length && !breaksOff" class="time-clock__breaks q-mt-xs">
        <div
          v-for="(item, index) in breaksLatestFirst"
          :key="item.id || index"
          :class="['time-clock__break', { 'time-clock__break--open': item.isOpen, 'time-clock__break--cancelled': item.isCancelled }]"
        >
          <template v-if="item.isCancelled">
            <q-icon name="o_cancel" size="16px" />
            <span class="time-clock__struck ellipsis">{{ item.reason }}</span>
            <span class="time-clock__hint">{{ item.startText }}</span>
            <q-badge color="grey-4" text-color="grey-9">Cancelled</q-badge>
            <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">
              Started at {{ item.startText }} and then cancelled{{ item.cancelReason ? `: ${item.cancelReason}` : "" }}.
              It counts 0 mins.
            </q-tooltip>
            <span class="text-weight-medium">0 mins</span>
            <span v-if="item.cancelReason" class="time-clock__hint time-clock__cancel-reason">{{ item.cancelReason }}</span>
          </template>
          <!-- Lunch, dinner and gate pass: an amount logged at once, with no timer; changed or cancelled here. -->
          <template v-else-if="isLoggedAmount(item)">
            <q-icon :name="item.isGatePass ? 'o_directions_walk' : item.isDinner ? 'o_dinner_dining' : 'o_restaurant'" size="16px" />
            <span class="text-weight-medium ellipsis">{{ item.reason }}</span>
            <span class="time-clock__hint">added {{ item.startText }}</span>
            <span class="text-weight-medium">{{ duration(item.seconds) }}</span>
            <q-badge v-if="item.isAddedLater" color="blue-1" text-color="primary">
              <q-icon name="o_edit_note" size="13px" class="q-mr-xs" />
              Manual
            </q-badge>
            <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">{{ breakTip(item) }}</q-tooltip>
            <template v-if="canLogAmounts">
              <q-btn v-if="item.isGatePass" flat round dense size="sm" color="primary" icon="o_edit" :disable="!!busy" @click="openGatePass(item)">
                <q-tooltip>Change the minutes or the purpose</q-tooltip>
              </q-btn>
              <q-btn flat round dense size="sm" color="grey-8" icon="o_cancel" :disable="!!busy" @click="onCancelLogged(item)">
                <q-tooltip>Cancel this {{ item.isGatePass ? "gate pass" : "break" }} (it will count 0 mins)</q-tooltip>
              </q-btn>
            </template>
          </template>
          <template v-else>
            <q-icon :name="item.isOpen ? 'o_pause_circle' : item.isGatePass ? 'o_directions_walk' : 'o_free_breakfast'" size="16px" />
            <span class="text-weight-medium ellipsis">{{ item.reason }}</span>
            <span class="time-clock__hint">{{ item.startText }} – {{ item.isOpen ? "now" : item.endText }}</span>
            <span class="text-weight-medium">{{ duration(item.isOpen ? item.seconds + elapsed : item.seconds) }}</span>
            <q-badge v-if="item.isAddedLater" color="blue-1" text-color="primary">
              <q-icon name="o_edit_note" size="13px" class="q-mr-xs" />
              Manual
            </q-badge>
            <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">{{ breakTip(item) }}</q-tooltip>
          </template>
        </div>
      </div>
    </q-card-section>

    <!-- Gate pass: a purpose and a slider of minutes, up to what is left of the day's two hours. -->
    <q-dialog v-model="gpOpen" persistent>
      <q-card style="width: 460px; max-width: 92vw">
        <q-card-section class="row items-center no-wrap q-py-sm">
          <q-icon name="o_directions_walk" color="primary" size="20px" class="q-mr-xs" />
          <div class="text-subtitle1 text-weight-medium text-primary">{{ gp.id ? "Change Gate Pass" : "Add Gate Pass" }}</div>
          <q-space />
          <q-btn v-close-popup flat round dense icon="o_close" :disable="busy === 'gatePass'">
            <q-tooltip>Close</q-tooltip>
          </q-btn>
        </q-card-section>
        <q-separator />
        <q-form greedy @submit.prevent.stop="onSaveGatePass">
          <q-card-section>
            <div class="time-clock__hint q-mb-md">
              Choose how long you need. The minutes are logged straight away and come off your working hours; there
              is no timer. You can change or cancel it later, up to {{ duration(gatePassMax) }} of gate pass a day.
            </div>

            <div class="time-clock__label">Purpose <span class="text-negative">*</span></div>
            <q-input
              v-model="gp.purpose"
              outlined
              dense
              hide-bottom-space
              maxlength="80"
              placeholder="e.g. Bank work"
              aria-label="Purpose of the gate pass"
              :rules="[(value) => !!(value && value.trim()) || 'Purpose is required']"
            />

            <div class="row items-center q-mt-md">
              <div class="time-clock__label q-mb-none">Minutes</div>
              <q-space />
              <div class="time-clock__value">{{ duration(gp.minutes * 60) }}</div>
            </div>
            <q-slider
              v-model="gp.minutes"
              :min="gpStep"
              :max="gpMaxMinutes"
              :step="gpStep"
              color="primary"
              label
              :label-value="`${gp.minutes} mins`"
              markers
              :marker-labels="gpMarkerLabels"
              aria-label="Gate pass minutes"
              class="q-mt-sm q-px-sm"
            />
            <div class="time-clock__hint q-mt-lg">
              {{ duration(Math.max(0, gpMaxMinutes - gp.minutes) * 60) }} of gate pass left today after this.
            </div>
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn v-close-popup flat no-caps color="grey-8" label="Cancel" :disable="busy === 'gatePass'" />
            <q-btn unelevated no-caps color="primary" type="submit" :label="gp.id ? 'Update Gate Pass' : 'Add Gate Pass'" :loading="busy === 'gatePass'" />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>

    <q-dialog v-model="outOpen" persistent>
      <q-card style="width: 460px; max-width: 92vw">
        <q-card-section class="row items-center no-wrap q-py-sm">
          <q-icon name="o_logout" color="primary" size="20px" class="q-mr-xs" />
          <div class="text-subtitle1 text-weight-medium text-primary">Time Out</div>
          <q-space />
          <q-btn v-close-popup flat round dense icon="o_close" :disable="busy === 'out'">
            <q-tooltip>Close</q-tooltip>
          </q-btn>
        </q-card-section>
        <q-separator />
        <q-card-section>
          <q-banner v-if="status.timesheetMissing" dense rounded class="time-clock__warn q-mb-md">
            <template #avatar><q-icon name="o_event_note" /></template>
            Reminder: you have not filled your timesheet for today ({{ status.timesheetDateText }}). You can still
            time out.
            <template #action>
              <q-btn flat no-caps color="brown-9" label="Open Timesheet" :disable="busy === 'out'" @click="openTimesheet">
                <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">Closes this and takes you to Timesheets. You stay timed in.</q-tooltip>
              </q-btn>
            </template>
          </q-banner>

          <div>{{ outQuestion }}</div>

          <template v-if="mealsNotLogged.length">
            <div class="time-clock__label q-mt-md">No lunch or dinner break is logged for today. What did you take?</div>
            <q-option-group v-model="out.meal" :options="outMealOptions" color="primary" dense />
            <div class="time-clock__hint q-mt-xs">
              Choosing lunch or dinner takes {{ minutes(lunchAllowance) }} mins off, so your working hours are exact.
            </div>
          </template>

          <div class="time-clock__label q-mt-md">Note <span class="time-clock__hint">(optional)</span></div>
          <q-input
            v-model="out.note"
            type="textarea"
            outlined
            dense
            autogrow
            counter
            maxlength="500"
            placeholder="Anything you want to mention about today"
            aria-label="Time-out note"
          >
            <q-tooltip anchor="top middle" self="bottom middle" :delay="300" max-width="280px">
              Optional. For example why you are leaving early, or what you worked on. It is saved with today's
              Time In - Out entry.
            </q-tooltip>
          </q-input>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn v-close-popup flat no-caps color="grey-8" label="No" :disable="busy === 'out'" />
          <q-btn unelevated no-caps color="primary" label="Yes" :loading="busy === 'out'" @click="onConfirmTimeOut" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- A break that was taken but never started here: entered afterwards with its clock times. -->
    <q-dialog v-model="pastOpen" persistent>
      <q-card style="width: 440px; max-width: 92vw">
        <q-card-section class="row items-center no-wrap q-py-sm">
          <q-icon :name="past.lunch ? 'o_restaurant' : 'o_more_time'" color="primary" size="20px" class="q-mr-xs" />
          <div class="text-subtitle1 text-weight-medium text-primary">{{ past.lunch ? "Add Lunch / Dinner Break" : "Add Missed Break" }}</div>
          <q-space />
          <q-btn v-close-popup flat round dense icon="o_close" :disable="busy === 'breakAdd'">
            <q-tooltip>Close</q-tooltip>
          </q-btn>
        </q-card-section>
        <q-separator />
        <q-form greedy @submit.prevent.stop="onSavePastBreak">
          <q-card-section>
            <div class="time-clock__hint q-mb-md">
              {{ past.lunch ? "Choose lunch or dinner, then pick when you went out and when you came back." : "For a break you took but did not start here." }}
              It must fall between your time in
              ({{ status.timeInText }}) and {{ isCompleted ? `your time out (${status.timeOutText})` : "now" }}, and
              cannot overlap another break. It is saved as a manual entry.
            </div>

            <template v-if="past.lunch">
              <div class="time-clock__label">Meal break</div>
              <q-btn-toggle
                v-model="past.reason"
                unelevated
                no-caps
                spread
                toggle-color="primary"
                color="grey-3"
                text-color="grey-9"
                aria-label="Lunch or dinner"
                :options="[
                  { label: 'Lunch', value: 'Lunch', icon: 'o_restaurant' },
                  { label: 'Dinner', value: 'Dinner', icon: 'o_dinner_dining' }
                ]"
              />
            </template>

            <div v-if="!past.lunch" class="time-clock__label">Reason</div>
            <q-select
              v-if="!past.lunch"
              v-model="past.reason"
              outlined
              dense
              options-dense
              hide-bottom-space
              :options="pastReasonOptions"
              aria-label="Reason for the break"
            />
            <template v-if="!past.lunch && isGatePassReason(past.reason)">
              <div class="time-clock__label q-mt-md">Purpose of the gate pass <span class="text-negative">*</span></div>
              <q-input
                v-model="past.purpose"
                outlined
                dense
                hide-bottom-space
                maxlength="80"
                placeholder="e.g. Bank work"
                aria-label="Purpose of the gate pass"
                :hint="`${duration(gatePassLeft)} of gate pass left today`"
                :rules="[(value) => !!(value && value.trim()) || 'Purpose is required']"
              />
            </template>

            <div class="row q-col-gutter-md q-mt-xs">
              <div v-for="field in pastFields" :key="field.key" class="col-12 col-sm-6">
                <div class="time-clock__label">{{ field.label }} <span class="text-negative">*</span></div>
                <q-input
                  :model-value="clockText(past[field.key])"
                  readonly
                  outlined
                  dense
                  hide-bottom-space
                  class="cursor-pointer"
                  placeholder="Pick a time"
                  :aria-label="field.hint"
                  :hint="field.hint"
                  :rules="[() => !!past[field.key] || `${field.label} is required`]"
                  @click="pickers[field.key]?.show()"
                >
                  <template #append>
                    <q-icon name="o_access_time" class="cursor-pointer">
                      <q-popup-proxy :ref="(el) => { pickers[field.key] = el; }" cover transition-show="scale" transition-hide="scale">
                        <q-time
                          :model-value="past[field.key]"
                          mask="HH:mm"
                          @update:model-value="(value) => onPickPastTime(field.key, value)"
                        >
                          <div class="row items-center justify-end">
                            <q-btn v-close-popup flat no-caps color="primary" label="Done" />
                          </div>
                        </q-time>
                      </q-popup-proxy>
                    </q-icon>
                  </template>
                </q-input>
              </div>
            </div>
            <div v-if="pastLength" class="time-clock__hint q-mt-sm">
              Length: <span class="text-weight-medium">{{ pastLength }}</span>
            </div>
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn v-close-popup flat no-caps color="grey-8" label="Cancel" :disable="busy === 'breakAdd'" />
            <q-btn unelevated no-caps color="primary" type="submit" label="Add Break" :loading="busy === 'breakAdd'" />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>
  </q-card>
</template>

<script setup>
// The dashboard's Time In - Out: time in, any number of breaks, time out, with the working hours
// (total minus breaks) counting live. The server owns every figure and every rule (TimeClockRules.cs);
// between its answers this card only adds the seconds that have passed to whichever counter is running.
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useQuasar } from "quasar";
import { useRouter } from "vue-router";
import { useConfirm } from "composables/useConfirm";
import { useNotify } from "composables/useNotify";
import commonService from "services/common.service";
import timeInTimeOutService from "modules/timeInTimeOut/timeInTimeOut.service";

const RESYNC_MS = 60000;
const MAX_SESSION_SECONDS = 24 * 3600 - 1;
const DEFAULT_REASONS = ["Lunch", "Tea / Coffee", "Personal"];

const $q = useQuasar();
const router = useRouter();
const { confirm } = useConfirm();
const notify = useNotify();

const emptyStatus = () => ({
  state: "NotStarted",
  sessionId: null,
  hasEmployee: true,
  isOverdue: false,
  serverNowMs: 0,
  timeInMs: null,
  timeOutMs: null,
  timeInDateText: "",
  timeInText: "",
  timeOutDateText: "",
  timeOutText: "",
  grossSeconds: 0,
  breakSeconds: 0,
  workedSeconds: 0,
  workedText: "",
  timeOutNote: "",
  timesheetMissing: false,
  timesheetDateText: "",
  pendingTimesheets: [],
  onlyTimeInTimeOut: false,
  breakText: "",
  lunchSeconds: 0,
  lunchAllowanceSeconds: 0,
  dinnerSeconds: 0,
  lunchLogged: false,
  dinnerLogged: false,
  breakUsedSeconds: 0,
  breakStandardSeconds: 0,
  gatePassSeconds: 0,
  gatePassMaxSeconds: 0,
  breaks: []
});

const loading = ref(true);
const busy = ref("");
const status = ref(emptyStatus());
const breakReasons = ref(DEFAULT_REASONS);
const otherReason = ref("");
const breakMenu = ref(null);
const pastOpen = ref(false);
const outOpen = ref(false);
const out = ref({ meal: "", note: "" });
const past = ref({ lunch: false, reason: "Lunch", purpose: "", start: "", end: "" });
// Break Out is the moment of leaving and Break In the moment of coming back, as the office says it.
const pastFields = [
  { key: "start", label: "Break Out", hint: "When you went out" },
  { key: "end", label: "Break In", hint: "When you came back" }
];
const pickers = {};

// Whole seconds since the server's last answer, by this device's clock. Only the difference is used, so
// a device clock that is wrong (or in another time zone) does not matter.
const receivedAt = ref(Date.now());
const tick = ref(Date.now());
const elapsed = computed(() => Math.max(0, Math.floor((tick.value - receivedAt.value) / 1000)));

const isNotStarted = computed(() => status.value.state === "NotStarted");
const isWorking = computed(() => status.value.state === "Working");
const isOnBreak = computed(() => status.value.state === "OnBreak");
const isCompleted = computed(() => status.value.state === "Completed");
// Time-in / time-out only mode (the API's appsettings.json, TimeClock:OnlyTimeInTimeOut): every break action
// and the whole Breaks section are hidden, End Break included; Time Out ends a break still running.
const breaksOff = computed(() => !!status.value.onlyTimeInTimeOut);

// Working: total and working time run. On break: total and break run, working time stands still.
const live = computed(() => {
  const s = status.value;
  if (isNotStarted.value || isCompleted.value) {
    return { gross: s.grossSeconds, break: s.breakSeconds, worked: s.workedSeconds };
  }
  const gross = Math.min(s.grossSeconds + elapsed.value, MAX_SESSION_SECONDS);
  const added = gross - s.grossSeconds;
  return {
    gross,
    break: s.breakSeconds + (isOnBreak.value ? added : 0),
    worked: s.workedSeconds + (isWorking.value ? added : 0)
  };
});

const stateBadge = computed(() => {
  if (isWorking.value) return { label: "Working", icon: "o_play_circle", color: "positive", textColor: "white", tip: "You are timed in and your working hours are counting." };
  if (onGatePassNow.value) return { label: "On gate pass", icon: "o_directions_walk", color: "warning", textColor: "dark", tip: "You are out on a gate pass. Working hours are paused until you click End Gate Pass." };
  if (isOnBreak.value) return { label: "On break", icon: "o_pause_circle", color: "warning", textColor: "dark", tip: "You are on a break. Working hours are paused until you click End Break." };
  if (isCompleted.value) return { label: "Timed out", icon: "o_check_circle", color: "blue-grey-6", textColor: "white", tip: "You have timed out. These are the saved hours of this session." };
  return { label: "Not timed in", icon: "o_radio_button_unchecked", color: "grey-4", textColor: "grey-9", tip: "You have not timed in today. Click Time In to start." };
});

const heroHint = computed(() => {
  if (isOnBreak.value) return onGatePassNow.value ? "Paused while you are on a gate pass" : "Paused while you are on a break";
  if (isWorking.value) return breaksOff.value ? "Counting since your time in" : "Counting, breaks excluded";
  if (isCompleted.value) return breaksOff.value ? "Final for this session" : "Final for this session, breaks excluded";
  return "Time in to start your day";
});

// Cancelled breaks are listed but are not breaks taken.
const breakCountText = computed(() => {
  const real = status.value.breaks.filter((item) => !item.isGatePass);
  const taken = real.filter((item) => !item.isCancelled).length;
  const cancelled = real.length - taken;
  const text = !taken ? "No breaks" : taken === 1 ? "1 break" : `${taken} breaks`;
  return cancelled ? `${text}, ${cancelled} cancelled` : text;
});

// The date is only worth showing when it is not the obvious one: a session that began on another day.
const showTimeInDate = computed(() =>
  !!status.value.timeInDateText && (status.value.isOverdue || (!!status.value.timeOutDateText && status.value.timeOutDateText !== status.value.timeInDateText))
);
const showTimeOutDate = computed(() =>
  !!status.value.timeOutDateText && status.value.timeOutDateText !== status.value.timeInDateText
);

// Latest first: the break in progress, or the one just taken, leads the list.
const breaksLatestFirst = computed(() => [...status.value.breaks].sort((a, b) => b.startMs - a.startMs));

// Lunch: the breaks whose reason says lunch, set against the daily allowance the server states.
const isLunchReason = (reason) => /lunch/i.test(reason || "");
const isMealReason = (reason) => /lunch|dinner/i.test(reason || "");
// One lunch and one dinner a day: a meal with a break on the day cannot be taken again.
const mealLogged = computed(() => status.value.lunchLogged || status.value.dinnerLogged);
const isMealTaken = (reason) => isMealReason(reason) && mealLogged.value;
const lunchAllowance = computed(() => status.value.lunchAllowanceSeconds || 0);
const onLunchNow = computed(() => status.value.breaks.some((item) => item.isOpen && item.isLunch));
const onDinnerNow = computed(() => status.value.breaks.some((item) => item.isOpen && item.isDinner));
const runningSeconds = computed(() => live.value.gross - status.value.grossSeconds);
const liveLunch = computed(() => status.value.lunchSeconds + (onLunchNow.value ? runningSeconds.value : 0));
const liveDinner = computed(() => status.value.dinnerSeconds + (onDinnerNow.value ? runningSeconds.value : 0));
// How far a lunch or dinner in progress has run past its allowance.
const mealOver = computed(() => {
  const running = onLunchNow.value ? liveLunch.value : onDinnerNow.value ? liveDinner.value : 0;
  return lunchAllowance.value ? Math.max(0, running - lunchAllowance.value) : 0;
});

// What Time Out will ask about: the day's meal break, when neither lunch nor dinner is on the day.
const mealsNotLogged = computed(() => {
  if (breaksOff.value || !lunchAllowance.value || isNotStarted.value || isCompleted.value || mealLogged.value) return [];
  return ["Lunch", "Dinner"];
});

function breakTip (item) {
  const what = item.isGatePass ? "Gate pass" : item.isLunch ? "Lunch break" : item.isDinner ? "Dinner break" : "Break";
  if (item.isOpen) return `${what} in progress since ${item.startText}. Click End Break when you are back.`;
  const length = duration(item.seconds);
  // Lunch, dinner and gate pass have no start and end: an amount, added at a moment.
  if (isLoggedAmount(item) && !item.isAddedLater) {
    return `${what} of ${length}, added at ${item.startText} with no timer and taken off your working hours.`;
  }
  return item.isAddedLater
    ? `${what} of ${length}. Manual entry: added by hand afterwards, not timed as it happened.`
    : `${what} from ${item.startText} to ${item.endText}: ${length}, taken off your working hours.`;
}

// Gate pass: time out of the office, told by its reason like the meals, with a daily maximum.
const isGatePassReason = (reason) => /gate[\s-]*pass/i.test(reason || "");
const onGatePassNow = computed(() => status.value.breaks.some((item) => item.isOpen && item.isGatePass));
// What the running thing is called on the buttons and in the questions.
const awayName = computed(() => (onGatePassNow.value ? "Gate Pass" : "Break"));

const gatePassMax = computed(() => status.value.gatePassMaxSeconds || 0);
const liveGatePass = computed(() => status.value.gatePassSeconds + (onGatePassNow.value ? runningSeconds.value : 0));
const gatePassLeft = computed(() => Math.max(0, gatePassMax.value - liveGatePass.value));
const showGatePass = computed(() => gatePassMax.value > 0 && (liveGatePass.value > 0 || onGatePassNow.value));
const gatePassHint = computed(() => {
  const over = liveGatePass.value - gatePassMax.value;
  if (over >= 60) return `Over the ${duration(gatePassMax.value)} by ${duration(over)}`;
  return gatePassLeft.value >= 60 ? `${duration(gatePassLeft.value)} left of ${duration(gatePassMax.value)}` : "Fully used";
});

// Breaks against the day's standard. Going over is allowed, so this only informs.
const breakStandard = computed(() => status.value.breakStandardSeconds || 0);
const liveBreakUsed = computed(() =>
  status.value.breakUsedSeconds + (isOnBreak.value && !onGatePassNow.value ? runningSeconds.value : 0)
);
const breakOver = computed(() => (breakStandard.value ? Math.max(0, liveBreakUsed.value - breakStandard.value) : 0));
const breakHint = computed(() => {
  if (!breakStandard.value) return breakCountText.value;
  if (breakOver.value >= 60) return `${duration(breakOver.value)} over the ${minutes(breakStandard.value)} mins standard`;
  const left = breakStandard.value - liveBreakUsed.value;
  return left >= 60 ? `${duration(left)} left of ${minutes(breakStandard.value)} mins` : `${minutes(breakStandard.value)} mins standard used`;
});

// A reason that cannot be picked now: a meal when the day already has its lunch or dinner.
const isReasonBlocked = (reason) => isMealTaken(reason);

// The note beside a reason in the Start Break list.
function reasonCaption (reason) {
  if (isMealTaken(reason)) return status.value.lunchLogged ? "Lunch taken today" : "Dinner taken today";
  if (isMealReason(reason)) return lunchAllowance.value ? `${minutes(lunchAllowance.value)} mins, no timer` : "";
  return "";
}

// "Lunch" and "Dinner" always lead the reasons, whatever the maintained list calls them. Gate pass is not
// a reason: it has its own button and its own dialog.
const reasonOptions = computed(() => [
  "Lunch",
  "Dinner",
  ...breakReasons.value.filter((reason) => !isMealReason(reason) && !isGatePassReason(reason))
]);

// Lunch, dinner and gate pass are amounts logged at once (no timer); only while timed in.
const canLogAmounts = computed(() => (isWorking.value || isOnBreak.value) && !status.value.isOverdue && !breaksOff.value);
const isLoggedAmount = (item) => !item.isOpen && !item.isCancelled && (item.isGatePass || item.isLunch || item.isDinner);
const gatePassMinutesLeft = computed(() => Math.floor(gatePassLeft.value / 60));

// The gate pass dialog: a new one, or one from the list being changed (its own minutes are free to reuse).
const gpOpen = ref(false);
const gp = ref({ id: null, minutes: 30, purpose: "", own: 0 });
const gpMaxMinutes = computed(() => gatePassMinutesLeft.value + gp.value.own);
// Steps of 5 minutes, or of 1 when fewer than 5 are left.
const gpStep = computed(() => (gpMaxMinutes.value >= 5 ? 5 : 1));
const gpMarkerLabels = computed(() => {
  const max = gpMaxMinutes.value;
  const labels = {};
  for (let value = 30; value < max; value += 30) labels[value] = `${value}`;
  labels[gpStep.value] = `${gpStep.value}`;
  labels[max] = `${max}`;
  return labels;
});

// Said once, at the moment a running gate pass reaches the daily maximum.
watch(() => onGatePassNow.value && gatePassMax.value > 0 && gatePassLeft.value <= 0, (used, was) => {
  if (used && !was && elapsed.value > 0) {
    notify.warning(`You have used your ${duration(gatePassMax.value)} of gate pass for today. Please end the gate pass.`);
  }
});

// Said once, at the moment a running lunch or dinner passes its allowance (not when the page opens on one already over).
watch(() => mealOver.value > 0, (over, was) => {
  if (over && !was && elapsed.value > 0) {
    notify.warning(`Your ${minutes(lunchAllowance.value)} mins ${onDinnerNow.value ? "dinner" : "lunch"} break is over.`);
  }
});

const minutes = (seconds) => Math.floor(Math.max(0, seconds || 0) / 60);

// "13:05" from a time field as 01:05 PM, the way the card writes every other time.
function clockText (value) {
  // Nothing picked yet is an empty field, not midnight.
  const match = /^(\d{1,2}):(\d{2})/.exec(value || "");
  if (!match) return "";
  const hour = Number(match[1]);
  const pad = (part) => String(part).padStart(2, "0");
  return `${pad(hour % 12 || 12)}:${match[2]} ${hour < 12 ? "AM" : "PM"}`;
}

// Shown while typing; a "To" earlier than "From" is a break over midnight, which the server judges.
const pastLength = computed(() => {
  const toMinutes = (value) => {
    const match = /^(\d{1,2}):(\d{2})/.exec(value || "");
    return match ? Number(match[1]) * 60 + Number(match[2]) : null;
  };
  const from = toMinutes(past.value.start);
  const to = toMinutes(past.value.end);
  if (from == null || to == null || to <= from) return "";
  return duration((to - from) * 60);
});

// 04:12:35 for the running counters.
function clock (seconds) {
  const total = Math.max(0, Math.floor(seconds || 0));
  const pad = (value) => String(value).padStart(2, "0");
  return `${pad(Math.floor(total / 3600))}:${pad(Math.floor((total % 3600) / 60))}:${pad(total % 60)}`;
}

// "7 hr 42 mins" for sentences; under a minute is said in seconds so a short break is not "0 mins".
function duration (seconds) {
  const total = Math.max(0, Math.floor(seconds || 0));
  if (total < 60) return `${total} sec`;
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  if (hours && minutes) return `${hours} hr ${minutes} mins`;
  return hours ? `${hours} hr` : `${minutes} mins`;
}

function apply (resp) {
  status.value = { ...emptyStatus(), ...resp, breaks: resp?.breaks || [] };
  receivedAt.value = Date.now();
  tick.value = receivedAt.value;
}

async function refresh (silent = false) {
  try {
    apply(await timeInTimeOutService.getMyTimeClockStatus(silent));
  } catch {
    // The request layer has already said why; the card keeps what it last knew.
  } finally {
    loading.value = false;
  }
}

// One action at a time. Every action answers with the fresh status; when the server refuses one (the
// state changed in another tab or on another device), the card reloads to show what is true now.
async function run (name, action, message) {
  if (busy.value) return false;
  busy.value = name;
  try {
    apply(await action());
    notify.success(message(status.value));
    return true;
  } catch {
    await refresh();
    return false;
  } finally {
    busy.value = "";
  }
}

// Days worked, this week and last, whose timesheet is still to be filled; latest first.
const pendingTimesheets = computed(() => status.value.pendingTimesheets || []);
const timesheetReminderLabel = computed(() => {
  const days = pendingTimesheets.value;
  return days.length === 1 ? `Timesheet ${days[0].dateText} not filled` : `${days.length} timesheets not filled`;
});
const timesheetReminderText = computed(() => {
  const days = pendingTimesheets.value;
  return days.length === 1
    ? `your timesheet for ${days[0].dateText} is not filled yet.`
    : `your timesheets for ${days.length} days are not filled yet.`;
});

function openTimesheet () {
  outOpen.value = false;
  router.push("/timesheet");
}

// Timing in also reminds about the last day worked, when its timesheet is still empty. Only a reminder.
async function onTimeIn () {
  const timedIn = await run("in", () => timeInTimeOutService.timeIn(), (s) => `Timed in at ${s.timeInText}.`);
  if (timedIn && pendingTimesheets.value.length) {
    const text = timesheetReminderText.value;
    notify.warning(`Reminder: ${text}`, {
      timeout: 10000,
      actions: [
        { label: "Open Timesheet", color: "dark", noCaps: true, handler: openTimesheet },
        { label: "Later", color: "dark", noCaps: true }
      ]
    });
  }
}

function ask (title, message) {
  return confirm({ title, message, confirmLabel: "Yes", cancelLabel: "No" });
}

// Opens the gate pass dialog: empty for a new one, or on the gate pass picked from the list.
function openGatePass (item = null) {
  const own = item ? Math.round(item.seconds / 60) : 0;
  const max = gatePassMinutesLeft.value + own;
  gp.value = {
    id: item ? item.id : null,
    own,
    minutes: item ? own : Math.min(30, max),
    // The reason is stored as "Gate Pass: <purpose>".
    purpose: item ? item.reason.replace(/^gate[\s-]*pass\s*:?\s*/i, "") : ""
  };
  gpOpen.value = true;
}

async function onSaveGatePass () {
  const { id, minutes: chosen } = gp.value;
  const purpose = gp.value.purpose.trim();
  if (!purpose || chosen <= 0) return;

  const question = id
    ? `Are you sure you want to change this gate pass to ${duration(chosen * 60)}?`
    : `Are you sure you want to add a gate pass of ${duration(chosen * 60)}? It will be taken off your working hours straight away.`;
  if (!await ask(id ? "Change gate pass" : "Add gate pass", question)) return;

  const saved = await run(
    "gatePass",
    () => (id
      ? timeInTimeOutService.updateGatePass(status.value.sessionId, id, { minutes: chosen, purpose })
      : timeInTimeOutService.logGatePass(status.value.sessionId, { minutes: chosen, purpose })),
    () => (id ? "Gate pass updated." : `Gate pass of ${duration(chosen * 60)} added.`)
  );
  if (saved) gpOpen.value = false;
}

// Cancelling a logged lunch, dinner or gate pass: a reason, then it stays in the list as cancelled, 0 mins.
function onCancelLogged (item) {
  const what = item.isGatePass ? "gate pass" : `${item.isDinner ? "dinner" : "lunch"} break`;
  $q.dialog({
    title: `Cancel ${what}`,
    message: `Are you sure you want to cancel this ${what} of ${duration(item.seconds)}? It will be saved as 0 mins and shown ` +
      "as cancelled, and that time will count as working hours again. Reason for cancelling:",
    prompt: {
      model: "",
      type: "text",
      outlined: true,
      dense: true,
      maxlength: 100,
      placeholder: "e.g. Added by mistake",
      "aria-label": "Reason for cancelling",
      isValid: (value) => !!value && !!value.trim()
    },
    ok: { label: "Yes", unelevated: true, noCaps: true, color: "primary" },
    cancel: { label: "No", flat: true, noCaps: true, color: "grey-8" },
    persistent: true
  }).onOk((reason) => {
    run(
      "breakCancel",
      () => timeInTimeOutService.cancelLoggedBreak(status.value.sessionId, item.id, reason.trim()),
      () => `${what.charAt(0).toUpperCase()}${what.slice(1)} cancelled.`
    );
  });
}

async function onStartBreak (reason) {
  // Lunch and dinner are not timed: the full allowance is added at once.
  if (isMealReason(reason)) {
    const meal = /dinner/i.test(reason) ? "dinner" : "lunch";
    const allowance = minutes(lunchAllowance.value);
    const question = `Are you sure you want to add your ${meal} break? ${allowance} mins will be taken off your working hours ` +
      "straight away. There is no timer to end.";
    if (!await ask(`Add ${meal} break`, question)) return;
    run("breakStart", () => timeInTimeOutService.startBreak(status.value.sessionId, reason), () => `${meal.charAt(0).toUpperCase()}${meal.slice(1)} break of ${allowance} mins added.`);
    return;
  }
  if (!await ask("Start break", `Are you sure you want to start a break (${reason})? Your working hours will pause until you end it.`)) return;
  run("breakStart", () => timeInTimeOutService.startBreak(status.value.sessionId, reason), () => "Break started.");
}

function onStartOtherBreak () {
  const reason = otherReason.value.trim();
  if (!reason) return;
  breakMenu.value?.hide();
  onStartBreak(reason);
}

function runningBreakLength () {
  const running = status.value.breaks.find((item) => item.isOpen);
  return duration((running?.seconds || 0) + elapsed.value);
}

async function onEndBreak () {
  const name = awayName.value;
  if (!await ask(`End ${name.toLowerCase()}`, `Are you sure you want to end your ${name.toLowerCase()}? It has run for ${runningBreakLength()}.`)) return;
  const length = runningBreakLength();
  run("breakEnd", () => timeInTimeOutService.endBreak(status.value.sessionId), () => `${name} ended after ${length}.`);
}

// Cancelling needs a reason, so this question carries a field: Yes stays disabled until it is filled.
function onCancelBreak () {
  $q.dialog({
    title: `Cancel ${awayName.value.toLowerCase()}`,
    message: `Are you sure you want to cancel this ${awayName.value.toLowerCase()}? It will be saved as 0 mins and shown as cancelled, ` +
      "and the time since it started will count as working hours. Reason for cancelling:",
    prompt: {
      model: "",
      type: "text",
      outlined: true,
      dense: true,
      maxlength: 100,
      placeholder: "e.g. Started by mistake",
      "aria-label": "Reason for cancelling the break",
      isValid: (value) => !!value && !!value.trim()
    },
    ok: { label: "Yes", unelevated: true, noCaps: true, color: "primary" },
    cancel: { label: "No", flat: true, noCaps: true, color: "grey-8" },
    persistent: true
  }).onOk((reason) => {
    run("breakCancel", () => timeInTimeOutService.cancelBreak(status.value.sessionId, reason.trim()), () => "Break cancelled.");
  });
}

// "13:00" plus minutes, wrapping at midnight.
function addMinutes (value, count) {
  const [hour, minute] = value.split(":").map(Number);
  const total = (hour * 60 + minute + count) % 1440;
  const pad = (part) => String(part).padStart(2, "0");
  return `${pad(Math.floor(total / 60))}:${pad(total % 60)}`;
}

// Picking the Break Out of a lunch proposes its Break In, a full lunch later; the employee corrects it if not.
function onPickPastTime (key, value) {
  if (!value) return;
  past.value[key] = value;
  if (key === "start" && past.value.lunch && !past.value.end && lunchAllowance.value) {
    past.value.end = addMinutes(value, minutes(lunchAllowance.value));
  }
}

// A missed break is an ordinary break: lunch, dinner and gate pass are added from their own buttons.
const pastReasonOptions = computed(() => reasonOptions.value.filter((reason) => !isMealReason(reason)));

function openPastBreak (lunch) {
  const reasons = reasonOptions.value;
  // A missed break opens on the first reason that is not lunch; lunch has its own button.
  const other = reasons.find((reason) => !isMealReason(reason) && !isGatePassReason(reason));
  past.value = { lunch, reason: lunch ? reasons[0] : other || reasons[1], purpose: "", start: "", end: "" };
  pastOpen.value = true;
}

async function onSavePastBreak () {
  const { start, end } = past.value;
  if (!start || !end) return;

  const gatePass = !past.value.lunch && isGatePassReason(past.value.reason);
  if (gatePass && !past.value.purpose.trim()) return;
  const reason = gatePass ? `Gate Pass: ${past.value.purpose.trim()}` : past.value.reason;

  const question = `Are you sure you want to add a ${gatePass ? "gate pass" : `${reason} break`} with Break Out ${clockText(start)} and Break In ${clockText(end)}? ` +
    "It will be taken off your working hours.";
  if (!await ask("Add missed break", question)) return;

  const added = await run(
    "breakAdd",
    () => timeInTimeOutService.addPastBreak(status.value.sessionId, { startTime: start, endTime: end, breakReason: reason }),
    () => "Break added."
  );
  if (added) pastOpen.value = false;
}

// The Time Out question, as the dialog asks it.
const outQuestion = computed(() => {
  const breaks = live.value.break ? `, with ${duration(live.value.break)} of breaks` : "";
  return `Are you sure you want to time out? You have worked ${duration(live.value.worked)}${breaks}.` +
    (isOnBreak.value ? ` Your running ${awayName.value.toLowerCase()} will end now.` : "");
});

// Lunch or dinner, one of them, or neither: asked only when the day has no meal break.
const outMealOptions = computed(() => [
  ...mealsNotLogged.value.map((meal) => ({ label: `I took a ${meal.toLowerCase()} break (${minutes(lunchAllowance.value)} mins)`, value: meal })),
  { label: "I did not take a lunch or dinner break", value: "" }
]);

function onTimeOut () {
  out.value = { meal: "", note: "" };
  outOpen.value = true;
}

async function onConfirmTimeOut () {
  const { meal, note } = out.value;
  const done = await run(
    "out",
    () => timeInTimeOutService.timeOut(status.value.sessionId, { addLunch: meal === "Lunch", addDinner: meal === "Dinner", note }),
    (s) => `Timed out. Worked ${s.workedText}.`
  );
  // On a refusal the card has reloaded; the dialog closes either way so it never shows a stale question.
  outOpen.value = false;
  return done;
}

// The reasons HR maintains for Movement Register breaks; the built-in three stand in until they load.
function loadBreakReasons () {
  commonService.getDropDown("Break Explanation").then((resp) => {
    const names = (resp || []).map((item) => item.dropdownValue).filter(Boolean);
    if (names.length) breakReasons.value = names;
  }).catch(() => {});
}

function onVisible () {
  if (document.visibilityState === "visible") refresh(true);
}

let tickTimer = null;
let resyncTimer = null;

onMounted(() => {
  refresh();
  loadBreakReasons();
  tickTimer = setInterval(() => { tick.value = Date.now(); }, 1000);
  // Catches a time-in or a break made in another tab, and the day rolling over.
  resyncTimer = setInterval(() => { if (!busy.value && document.visibilityState === "visible") refresh(true); }, RESYNC_MS);
  document.addEventListener("visibilitychange", onVisible);
});

onBeforeUnmount(() => {
  clearInterval(tickTimer);
  clearInterval(resyncTimer);
  document.removeEventListener("visibilitychange", onVisible);
});
</script>

<style scoped>
.time-clock {
  border-radius: 12px;
}
.time-clock__state {
  padding: 4px 8px;
  border-radius: 12px;
  font-size: 12px;
}
.time-clock__body {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 16px 32px;
}
.time-clock__timer {
  font-size: 32px;
  font-weight: 600;
  line-height: 1.15;
  color: var(--q-primary);
  font-variant-numeric: tabular-nums;
}
.time-clock__stats {
  display: flex;
  flex-wrap: wrap;
  gap: 12px 28px;
  flex: 1 1 320px;
}
.time-clock__stat {
  min-width: 84px;
}
.time-clock__value {
  font-size: 17px;
  font-weight: 600;
  color: var(--ink-900);
  font-variant-numeric: tabular-nums;
}
.time-clock__value--break {
  color: #8a5300;
}
.time-clock__hint {
  font-size: 12px;
  color: var(--ink-500);
}
.time-clock__note {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 8px 10px;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: var(--brand-050, #f3f8fb);
  font-size: 13px;
  color: var(--ink-700);
  white-space: pre-wrap;
  word-break: break-word;
}
.time-clock__label {
  font-size: 13px;
  font-weight: 500;
  color: var(--ink-700);
  margin-bottom: 4px;
}
.time-clock__help {
  cursor: help;
}
.time-clock__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-left: auto;
}
.time-clock__head {
  row-gap: 4px;
}
.time-clock__reminder {
  background: #fff4e5;
  color: #8a5300;
  border-radius: 12px;
  padding: 0 8px;
}
.time-clock__warn {
  background: #fff4e5;
  color: #8a5300;
}
.time-clock__break.time-clock__break--cancelled {
  background: #f5f7f8;
  color: var(--ink-500);
}
.time-clock__struck {
  text-decoration: line-through;
}
.time-clock__cancel-reason {
  font-style: italic;
}
.time-clock__breaks {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.time-clock__break {
  display: flex;
  align-items: center;
  gap: 8px;
  max-width: 100%;
  padding: 4px 10px;
  border: 1px solid var(--line);
  border-radius: 8px;
  font-size: 13px;
  color: var(--ink-700);
}
.time-clock__break--open {
  border-color: var(--q-warning);
  background: #fff8e1;
}
@media (max-width: 599px) {
  .time-clock__actions {
    width: 100%;
    margin-left: 0;
  }
  .time-clock__actions > * {
    flex: 1 1 auto;
  }
}
</style>
