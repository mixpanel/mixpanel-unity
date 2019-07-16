#import <Foundation/Foundation.h>

@interface Mixpanel: NSObject
{

}
@end

@implementation Mixpanel

static Mixpanel *_sharedInstance;

+(Mixpanel*) sharedInstance
{
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        _sharedInstance = [[Mixpanel alloc] init];
    });
    return _sharedInstance;   
}

-(id)init
{
    self = [super init];
    return self;
}

-(string)getCarrier
{
    CTTelephonyNetworkInfo *info = [[CTTelephonyNetworkInfo alloc] init];
    CTCarrier *carrier = [info subscriberCellularProvider];
    return carrier.carrierName;
}

@end

extern "C"
{
    string GetCarrier()
    {
        return [[Mixpanel sharedInstance] getCarrier];
    }
}