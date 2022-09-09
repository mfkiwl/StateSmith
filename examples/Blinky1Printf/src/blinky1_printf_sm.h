// Autogenerated with StateSmith
#pragma once
#include <stdint.h>  // this ends up in the generated .h file

enum blinky1_printf_sm_event_id
{
    BLINKY1_PRINTF_SM_EVENT_ID_DO = 0, // The `do` event is special. State event handlers do not consume this event (ancestors all get it too) unless a transition occurs.
};

enum
{
    BLINKY1_PRINTF_SM_EVENT_ID_COUNT = 1
};

enum blinky1_printf_sm_state_id
{
    BLINKY1_PRINTF_SM_STATE_ID_ROOT = 0,
    BLINKY1_PRINTF_SM_STATE_ID_LED_OFF = 1,
    BLINKY1_PRINTF_SM_STATE_ID_LED_ON = 2,
};

enum
{
    BLINKY1_PRINTF_SM_STATE_ID_COUNT = 3
};

typedef struct blinky1_printf_sm blinky1_printf_sm;
typedef void (*blinky1_printf_sm_func)(blinky1_printf_sm* sm);

struct blinky1_printf_sm
{
    // Used internally by state machine. Feel free to inspect, but don't modify.
    enum blinky1_printf_sm_state_id state_id;
    
    // Used internally by state machine. Don't modify.
    blinky1_printf_sm_func ancestor_event_handler;
    
    // Used internally by state machine. Don't modify.
    blinky1_printf_sm_func current_event_handlers[BLINKY1_PRINTF_SM_EVENT_ID_COUNT];
    
    // Used internally by state machine. Don't modify.
    blinky1_printf_sm_func current_state_exit_handler;
    
    // User variables. Can be used for inputs, outputs, user variables...
    struct
    {
        uint32_t timer_started_at_ms;  // milliseconds
    } vars;
};

// State machine constructor. Must be called before start or dispatch event functions. Not thread safe.
void blinky1_printf_sm_ctor(blinky1_printf_sm* self);

// Starts the state machine. Must be called before dispatching events. Not thread safe.
void blinky1_printf_sm_start(blinky1_printf_sm* self);

// Dispatches an event to the state machine. Not thread safe.
void blinky1_printf_sm_dispatch_event(blinky1_printf_sm* self, enum blinky1_printf_sm_event_id event_id);

// Converts a state id to a string. Thread safe.
const char* blinky1_printf_sm_state_id_to_string(const enum blinky1_printf_sm_state_id id);
