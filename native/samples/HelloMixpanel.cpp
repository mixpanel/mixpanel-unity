#include <mixpanel/mixpanel.hpp>
#include <thread>
#include <iostream>

int main(int argc, const char* argv[])
{
    mixpanel::Mixpanel mp("05b7195383129757cbf5172dbc5f67e1");

    // send data every ten seconds
    mp.set_flush_interval(10);

    //mp.alias("tina");
    mp.register_("age", "20");
    mp.register_("gender", "female");

    mixpanel::Value params;
    params["foo"] = 10;
    mp.track("hello", params);
    
    mp.start_timed_event("one second");
    std::this_thread::sleep_for(std::chrono::seconds(1));
    mp.track("one second");
    
    mp.people.set_once("$created", mixpanel::Mixpanel::utc_now());
    mp.people.set_first_name("Pat");
    mp.people.set_last_name("Davis");
    mp.people.set_name("Pat Davis");
    mp.people.set_email("pat@mixpanel.com");
    mp.people.set_phone("555-555-5555");

    mp.people.track_charge(0.42, mixpanel::Value());

    // flush the queue
    mp.flush_queue();

    std::cout << "*** Super properties: " << mp.get_super_properties() << std::endl;

    std::cout << "Clearing all super properties..." << std::endl;
    mp.clear_super_properties();

    std::cout << "*** Super properties: " << mp.get_super_properties() << std::endl;
    
    std::cout << "Sleeping for 30 seconds to give the background worker time to send requests..." << std::endl;
    // give the background worker some time to send out the requests
    std::this_thread::sleep_for(std::chrono::seconds(20));

    return 0;
}
