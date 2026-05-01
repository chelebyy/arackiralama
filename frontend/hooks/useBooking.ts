import { useCallback, useEffect, useState } from 'react';
import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type {
  Customer,
  Driver,
  ReservationExtra,
  Vehicle,
  Office,
} from '@/lib/api/types';

interface BookingDates {
  pickupOfficeId: string;
  pickupOfficeName: string;
  pickupDate: string;
  pickupTime: string;
  returnOfficeId: string;
  returnOfficeName: string;
  returnDate: string;
  returnTime: string;
}

interface BookingVehicle {
  vehicleGroupId: string;
  vehicleName: string;
  vehicleImage: string;
  dailyPrice: number;
  groupName: string;
}

interface BookingExtras {
  items: ReservationExtra[];
  total: number;
}

interface BookingState {
  dates: BookingDates | null;
  vehicle: BookingVehicle | null;
  extras: BookingExtras;
  customer: Customer | null;
  driver: Driver | null;
  campaignCode: string | null;
  campaignDiscount: number | null;
  step: 'search' | 'vehicles' | 'extras' | 'details' | 'payment' | 'confirmation';
}

interface BookingActions {
  setDates: (dates: BookingDates) => void;
  setVehicle: (vehicle: BookingVehicle) => void;
  setExtras: (extras: ReservationExtra[]) => void;
  setCustomer: (customer: Customer) => void;
  setDriver: (driver: Driver) => void;
  setCampaign: (code: string | null, discount?: number | null) => void;
  setStep: (step: BookingState['step']) => void;
  clearBooking: () => void;
  goToNextStep: () => void;
  goToPreviousStep: () => void;
}

const initialState: BookingState = {
  dates: null,
  vehicle: null,
  extras: { items: [], total: 0 },
  customer: null,
  driver: null,
  campaignCode: null,
  campaignDiscount: null,
  step: 'search',
};

const steps: BookingState['step'][] = [
  'search',
  'vehicles',
  'extras',
  'details',
  'payment',
  'confirmation',
];

export const useBookingStore = create<BookingState & BookingActions>()(
  persist(
    (set, get) => ({
      ...initialState,

      setDates: (dates) => set({ dates }),

      setVehicle: (vehicle) => set({ vehicle }),

      setExtras: (items) => {
        const total = items.reduce((sum, item) => sum + item.totalPrice, 0);
        set({ extras: { items, total } });
      },

      setCustomer: (customer) => set({ customer }),

      setDriver: (driver) => set({ driver }),

      setCampaign: (code, discount = null) => set({
        campaignCode: code,
        campaignDiscount: discount,
      }),

      setStep: (step) => set({ step }),

      clearBooking: () => set(initialState),

      goToNextStep: () => {
        const currentStep = get().step;
        const currentIndex = steps.indexOf(currentStep);
        if (currentIndex < steps.length - 1) {
          set({ step: steps[currentIndex + 1] });
        }
      },

      goToPreviousStep: () => {
        const currentStep = get().step;
        const currentIndex = steps.indexOf(currentStep);
        if (currentIndex > 0) {
          set({ step: steps[currentIndex - 1] });
        }
      },
    }),
    {
      name: 'car-rental-booking-storage',
      partialize: (state) => ({
        dates: state.dates,
        vehicle: state.vehicle,
        extras: state.extras,
        customer: state.customer,
        driver: state.driver,
        campaignCode: state.campaignCode,
        campaignDiscount: state.campaignDiscount,
      }),
    }
  )
);

export function useBookingState() {
  const state = useBookingStore();

  return {
    dates: state.dates,
    vehicle: state.vehicle,
    extras: state.extras,
    customer: state.customer,
    driver: state.driver,
    campaignCode: state.campaignCode,
    campaignDiscount: state.campaignDiscount,
    step: state.step,
    isComplete: Boolean(
      state.dates &&
        state.vehicle &&
        state.customer &&
        state.driver
    ),
  };
}

export function useBookingActions() {
  const store = useBookingStore();

  const selectVehicle = useCallback(
    (vehicle: Vehicle, pickupOffice: Office, returnOffice: Office) => {
      store.setVehicle({
        vehicleGroupId: vehicle.id,
        vehicleName: vehicle.name,
        vehicleImage: vehicle.imageUrl,
        dailyPrice: vehicle.dailyPrice,
        groupName: vehicle.groupName,
      });
      store.goToNextStep();
    },
    [store]
  );

  const updateExtras = useCallback(
    (extras: ReservationExtra[]) => {
      store.setExtras(extras);
    },
    [store]
  );

  const updateCustomerDetails = useCallback(
    (customer: Customer, driver: Driver) => {
      store.setCustomer(customer);
      store.setDriver(driver);
    },
    [store]
  );

  const applyCampaign = useCallback(
    (code: string | null, discount?: number) => {
      store.setCampaign(code, discount ?? null);
    },
    [store]
  );

  const resetBooking = useCallback(() => {
    store.clearBooking();
  }, [store]);

  const navigateToStep = useCallback(
    (step: BookingState['step']) => {
      store.setStep(step);
    },
    [store]
  );

  return {
    selectVehicle,
    updateExtras,
    updateCustomerDetails,
    applyCampaign,
    resetBooking,
    navigateToStep,
    goToNextStep: store.goToNextStep,
    goToPreviousStep: store.goToPreviousStep,
    setDates: store.setDates,
  };
}

export function useBookingPersistence() {
  const [isHydrated, setIsHydrated] = useState(false);

  useEffect(() => {
    setIsHydrated(true);
  }, []);

  return { isHydrated };
}
