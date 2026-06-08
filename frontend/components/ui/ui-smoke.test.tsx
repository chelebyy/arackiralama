import React from "react";
import { afterAll, beforeAll, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "./accordion";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "./alert-dialog";
import { Alert, AlertDescription, AlertTitle } from "./alert";
import { AspectRatio } from "./aspect-ratio";
import { Avatar, AvatarFallback, AvatarImage, AvatarIndicator } from "./avatar";
import { Badge, badgeVariants } from "./badge";
import {
  Breadcrumb,
  BreadcrumbEllipsis,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "./breadcrumb";
import { Button, buttonVariants } from "./button";
import { ButtonGroup, ButtonGroupSeparator, ButtonGroupText } from "./button-group";
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "./card";
import { Calendar } from "./calendar";
import {
  Carousel,
  CarouselContent,
  CarouselItem,
  CarouselNext,
  CarouselPrevious,
} from "./carousel";
import {
  ChartContainer,
  ChartLegendContent,
  ChartTooltipContent,
} from "./chart";
import { Checkbox } from "./checkbox";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "./collapsible";
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
  CommandShortcut,
} from "./command";
import {
  ContextMenu,
  ContextMenuCheckboxItem,
  ContextMenuContent,
  ContextMenuGroup,
  ContextMenuItem,
  ContextMenuLabel,
  ContextMenuRadioGroup,
  ContextMenuRadioItem,
  ContextMenuSeparator,
  ContextMenuShortcut,
  ContextMenuSub,
  ContextMenuSubContent,
  ContextMenuSubTrigger,
  ContextMenuTrigger,
} from "./context-menu";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "./dialog";
import {
  Drawer,
  DrawerClose,
  DrawerContent,
  DrawerDescription,
  DrawerFooter,
  DrawerHeader,
  DrawerTitle,
  DrawerTrigger,
} from "./drawer";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from "./dropdown-menu";
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
} from "./empty";
import {
  Field,
  FieldContent,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
  FieldLegend,
  FieldSeparator,
  FieldSet,
  FieldTitle,
} from "./field";
import { HoverCard, HoverCardContent, HoverCardTrigger } from "./hover-card";
import { Input } from "./input";
import {
  InputGroup,
  InputGroupAddon,
  InputGroupButton,
  InputGroupInput,
  InputGroupText,
  InputGroupTextarea,
} from "./input-group";
import { InputOTP, InputOTPGroup, InputOTPSlot, InputOTPSeparator } from "./input-otp";
import {
  Item,
  ItemActions,
  ItemContent,
  ItemDescription,
  ItemFooter,
  ItemGroup,
  ItemHeader,
  ItemMedia,
  ItemSeparator,
  ItemTitle,
} from "./item";
import { Kbd, KbdGroup } from "./kbd";
import { Label } from "./label";
import {
  Menubar,
  MenubarCheckboxItem,
  MenubarContent,
  MenubarGroup,
  MenubarItem,
  MenubarLabel,
  MenubarMenu,
  MenubarRadioGroup,
  MenubarRadioItem,
  MenubarSeparator,
  MenubarShortcut,
  MenubarSub,
  MenubarSubContent,
  MenubarSubTrigger,
  MenubarTrigger,
} from "./menubar";
import {
  NativeSelect,
  NativeSelectOptGroup,
  NativeSelectOption,
} from "./native-select";
import {
  NavigationMenu,
  NavigationMenuContent,
  NavigationMenuIndicator,
  NavigationMenuItem,
  NavigationMenuLink,
  NavigationMenuList,
  NavigationMenuTrigger,
  NavigationMenuViewport,
} from "./navigation-menu";
import {
  Pagination,
  PaginationContent,
  PaginationEllipsis,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from "./pagination";
import { Popover, PopoverContent, PopoverTrigger } from "./popover";
import { Progress } from "./progress";
import { RadioGroup, RadioGroupItem } from "./radio-group";
import {
  Reel,
  ReelContent,
  ReelControls,
  ReelFooter,
  ReelHeader,
  ReelImage,
  ReelItem,
  ReelMuteButton,
  ReelNavigation,
  ReelNextButton,
  ReelOverlay,
  ReelPlayButton,
  ReelPreviousButton,
  ReelProgress,
} from "./reel";
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "./resizable";
import { ScrollArea, ScrollBar } from "./scroll-area";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectScrollDownButton,
  SelectScrollUpButton,
  SelectSeparator,
  SelectTrigger,
  SelectValue,
} from "./select";
import { Separator } from "./separator";
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "./sheet";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupAction,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarInput,
  SidebarInset,
  SidebarMenu,
  SidebarMenuAction,
  SidebarMenuBadge,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSkeleton,
  SidebarMenuSub,
  SidebarMenuSubButton,
  SidebarMenuSubItem,
  SidebarProvider,
  SidebarRail,
  SidebarSeparator,
  SidebarTrigger,
} from "./sidebar";
import { Skeleton } from "./skeleton";
import { Slider } from "./slider";
import { Toaster } from "./sonner";
import { Spinner } from "./spinner";
import { Switch } from "./switch";
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from "./table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "./tabs";
import {
  Timeline,
  TimelineContent,
  TimelineDate,
  TimelineHeader,
  TimelineIndicator,
  TimelineItem,
  TimelineSeparator,
  TimelineTitle,
} from "./timeline";
import { Toast, ToastAction, ToastDescription, ToastProvider, ToastTitle, ToastViewport } from "./toast";
import { Toggle, toggleVariants } from "./toggle";
import { ToggleGroup, ToggleGroupItem } from "./toggle-group";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "./tooltip";
import {
  BarsLoader,
  CircularLoader,
  ClassicLoader,
  DotsLoader,
  PromptLoader,
  PulseDotLoader,
  PulseLoader,
  TerminalLoader,
  TextBlinkLoader,
  TextDotsLoader,
  TextShimmerLoader,
  TypingLoader,
  WaveLoader,
} from "./custom/prompt/loader";

vi.mock("embla-carousel-react", () => ({
  default: () => [
    vi.fn(),
    {
      canScrollPrev: () => true,
      canScrollNext: () => true,
      scrollPrev: vi.fn(),
      scrollNext: vi.fn(),
      on: vi.fn(),
      off: vi.fn(),
    },
  ],
}));

vi.mock("recharts", () => ({
  Legend: () => null,
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="responsive-chart">{children}</div>
  ),
  Tooltip: () => null,
}));

class ResizeObserverStub {
  observe = vi.fn();
  unobserve = vi.fn();
  disconnect = vi.fn();
}

Object.defineProperty(window, "matchMedia", {
  writable: true,
  value: vi.fn().mockImplementation((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
});

Object.defineProperty(window, "ResizeObserver", {
  writable: true,
  value: ResizeObserverStub,
});

Element.prototype.scrollIntoView = vi.fn();

let focusSpy: ReturnType<typeof vi.spyOn>;

beforeAll(() => {
  focusSpy = vi.spyOn(HTMLElement.prototype, "focus").mockImplementation(() => undefined);
});

afterAll(() => {
  focusSpy.mockRestore();
});

describe("shared UI primitives", () => {
  it("renders static layout primitives with accessible content", () => {
    render(
      <div>
        <Alert>
          <AlertTitle>System ready</AlertTitle>
          <AlertDescription>All launch checks are visible.</AlertDescription>
        </Alert>
        <AspectRatio ratio={16 / 9}>
          <div>ratio content</div>
        </AspectRatio>
        <Avatar>
          <AvatarImage alt="Operator" src="/operator.png" />
          <AvatarFallback>OP</AvatarFallback>
          <AvatarIndicator variant="success" />
        </Avatar>
        <Badge variant="secondary">Verified</Badge>
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem>
              <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator />
            <BreadcrumbItem>
              <BreadcrumbPage>Reservations</BreadcrumbPage>
            </BreadcrumbItem>
            <BreadcrumbEllipsis />
          </BreadcrumbList>
        </Breadcrumb>
        <Button variant="outline">Save</Button>
        <ButtonGroup>
          <ButtonGroupText>Mode</ButtonGroupText>
          <Button>Daily</Button>
          <ButtonGroupSeparator />
          <Button>Weekly</Button>
        </ButtonGroup>
        <Card>
          <CardHeader>
            <CardTitle>Fleet</CardTitle>
            <CardDescription>Availability overview</CardDescription>
            <CardAction>
              <Button size="sm">Refresh</Button>
            </CardAction>
          </CardHeader>
          <CardContent>42 available vehicles</CardContent>
          <CardFooter>Updated now</CardFooter>
        </Card>
        <Empty>
          <EmptyHeader>
            <EmptyMedia variant="icon">E</EmptyMedia>
            <EmptyTitle>No rows</EmptyTitle>
            <EmptyDescription>Filters returned no results.</EmptyDescription>
          </EmptyHeader>
          <EmptyContent>
            <Button>Clear filters</Button>
          </EmptyContent>
        </Empty>
        <KbdGroup>
          <Kbd>Ctrl</Kbd>
          <Kbd>K</Kbd>
        </KbdGroup>
        <Separator />
        <Skeleton data-testid="skeleton" />
        <Spinner data-testid="spinner" />
      </div>,
    );

    expect(screen.getByText("System ready")).toBeInTheDocument();
    expect(screen.getByText("42 available vehicles")).toBeInTheDocument();
    expect(screen.getByText("No rows")).toBeInTheDocument();
    expect(screen.getByTestId("skeleton")).toBeInTheDocument();
    expect(screen.getByTestId("spinner")).toBeInTheDocument();
    expect(buttonVariants({ variant: "ghost", size: "sm" })).toContain("h-8");
    expect(badgeVariants({ variant: "outline" })).toContain("border");
    expect(toggleVariants({ variant: "outline" })).toContain("border");
  });

  it("renders form and input primitives", () => {
    render(
      <form>
        <FieldSet>
          <FieldLegend>Reservation filters</FieldLegend>
          <FieldGroup>
            <Field orientation="horizontal">
              <FieldContent>
                <FieldLabel htmlFor="q">Search</FieldLabel>
                <FieldTitle>Customer search</FieldTitle>
                <FieldDescription>Search by name or reservation code.</FieldDescription>
              </FieldContent>
              <Input id="q" placeholder="RAC-1001" />
            </Field>
            <FieldSeparator>or</FieldSeparator>
            <Field>
              <InputGroup>
                <InputGroupAddon>TRY</InputGroupAddon>
                <InputGroupInput aria-label="Amount" defaultValue="1200" />
                <InputGroupButton type="button">Apply</InputGroupButton>
                <InputGroupText>daily</InputGroupText>
              </InputGroup>
              <InputGroupTextarea aria-label="Notes" defaultValue="Airport delivery" />
              <FieldError errors={[{ message: "Sample validation" }]} />
            </Field>
            <Label htmlFor="status">Status</Label>
            <NativeSelect id="status" defaultValue="confirmed">
              <NativeSelectOptGroup label="Active">
                <NativeSelectOption value="confirmed">Confirmed</NativeSelectOption>
              </NativeSelectOptGroup>
            </NativeSelect>
            <Checkbox aria-label="Paid" defaultChecked />
            <Switch aria-label="Notifications" defaultChecked />
            <RadioGroup defaultValue="airport">
              <RadioGroupItem value="airport" aria-label="Airport" />
            </RadioGroup>
            <Slider defaultValue={[40]} max={100} aria-label="Progress" />
            <InputOTP maxLength={6} value="123">
              <InputOTPGroup>
                <InputOTPSlot index={0} />
                <InputOTPSlot index={1} />
                <InputOTPSeparator />
                <InputOTPSlot index={2} />
              </InputOTPGroup>
            </InputOTP>
            <Select open value="confirmed">
              <SelectTrigger aria-label="Reservation state">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectScrollUpButton />
                <SelectGroup>
                  <SelectLabel>Active</SelectLabel>
                  <SelectItem value="confirmed">Confirmed</SelectItem>
                </SelectGroup>
                <SelectSeparator />
                <SelectScrollDownButton />
              </SelectContent>
            </Select>
          </FieldGroup>
        </FieldSet>
      </form>,
    );

    expect(screen.getByText("Reservation filters")).toBeInTheDocument();
    expect(screen.getByLabelText("Amount")).toHaveValue("1200");
    expect(screen.getByText("Sample validation")).toBeInTheDocument();
    expect(screen.getAllByText("Confirmed").length).toBeGreaterThan(0);
  });

  it("renders collection primitives", () => {
    render(
      <div>
        <Calendar mode="single" selected={new Date(2026, 4, 17)} />
        <Carousel>
          <CarouselContent>
            <CarouselItem>Slide one</CarouselItem>
          </CarouselContent>
          <CarouselPrevious />
          <CarouselNext />
        </Carousel>
        <Accordion type="single" collapsible defaultValue="one">
          <AccordionItem value="one">
            <AccordionTrigger>Pickup details</AccordionTrigger>
            <AccordionContent>Hotel lobby at 10:00</AccordionContent>
          </AccordionItem>
        </Accordion>
        <Collapsible open>
          <CollapsibleTrigger>Toggle details</CollapsibleTrigger>
          <CollapsibleContent>Visible details</CollapsibleContent>
        </Collapsible>
        <Command>
          <CommandInput placeholder="Search commands" />
          <CommandList>
            <CommandEmpty>No commands</CommandEmpty>
            <CommandGroup heading="Reservations">
              <CommandItem>Open reservation</CommandItem>
              <CommandSeparator />
              <CommandItem>
                Refund
                <CommandShortcut>R</CommandShortcut>
              </CommandItem>
            </CommandGroup>
          </CommandList>
        </Command>
        <ItemGroup>
          <Item>
            <ItemHeader>
              <ItemMedia variant="icon">V</ItemMedia>
              <ItemContent>
                <ItemTitle>Vehicle assigned</ItemTitle>
                <ItemDescription>Compact automatic</ItemDescription>
              </ItemContent>
              <ItemActions>
                <Button size="sm">Open</Button>
              </ItemActions>
            </ItemHeader>
            <ItemFooter>Due today</ItemFooter>
          </Item>
          <ItemSeparator />
        </ItemGroup>
        <Pagination>
          <PaginationContent>
            <PaginationItem>
              <PaginationPrevious href="?page=1" />
            </PaginationItem>
            <PaginationItem>
              <PaginationLink href="?page=2" isActive>
                2
              </PaginationLink>
            </PaginationItem>
            <PaginationItem>
              <PaginationEllipsis />
            </PaginationItem>
            <PaginationItem>
              <PaginationNext href="?page=3" />
            </PaginationItem>
          </PaginationContent>
        </Pagination>
        <Table>
          <TableCaption>Reservations</TableCaption>
          <TableHeader>
            <TableRow>
              <TableHead>Code</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow>
              <TableCell>RAC-1001</TableCell>
            </TableRow>
          </TableBody>
          <TableFooter>
            <TableRow>
              <TableCell>Total</TableCell>
            </TableRow>
          </TableFooter>
        </Table>
        <Tabs defaultValue="overview">
          <TabsList>
            <TabsTrigger value="overview">Overview</TabsTrigger>
          </TabsList>
          <TabsContent value="overview">Overview panel</TabsContent>
        </Tabs>
        <Timeline defaultValue={2}>
          <TimelineItem step={1}>
            <TimelineHeader>
              <TimelineSeparator />
              <TimelineDate>09:00</TimelineDate>
              <TimelineTitle>Created</TimelineTitle>
              <TimelineIndicator />
            </TimelineHeader>
            <TimelineContent>Reservation opened</TimelineContent>
          </TimelineItem>
        </Timeline>
      </div>,
    );

    expect(screen.getByText("Hotel lobby at 10:00")).toBeInTheDocument();
    expect(screen.getByText("Slide one")).toBeInTheDocument();
    expect(screen.getByText("Open reservation")).toBeInTheDocument();
    expect(screen.getByText("RAC-1001")).toBeInTheDocument();
    expect(screen.getByText("Overview panel")).toBeInTheDocument();
    expect(screen.getByText("Reservation opened")).toBeInTheDocument();
  });

  it("renders overlay primitives in open state", () => {
    render(
      <div>
        <AlertDialog open>
          <AlertDialogTrigger>Delete</AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Delete reservation</AlertDialogTitle>
              <AlertDialogDescription>This action is audited.</AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction>Continue</AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
        <Dialog open>
          <DialogTrigger>Edit</DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Edit reservation</DialogTitle>
              <DialogDescription>Change delivery details.</DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <DialogClose>Close</DialogClose>
            </DialogFooter>
          </DialogContent>
        </Dialog>
        <Drawer open>
          <DrawerTrigger>Open drawer</DrawerTrigger>
          <DrawerContent>
            <DrawerHeader>
              <DrawerTitle>Drawer title</DrawerTitle>
              <DrawerDescription>Drawer description</DrawerDescription>
            </DrawerHeader>
            <DrawerFooter>
              <DrawerClose>Close drawer</DrawerClose>
            </DrawerFooter>
          </DrawerContent>
        </Drawer>
        <Sheet open>
          <SheetTrigger>Open sheet</SheetTrigger>
          <SheetContent>
            <SheetHeader>
              <SheetTitle>Sheet title</SheetTitle>
              <SheetDescription>Sheet description</SheetDescription>
            </SheetHeader>
            <SheetFooter>
              <SheetClose>Close sheet</SheetClose>
            </SheetFooter>
          </SheetContent>
        </Sheet>
        <Popover open>
          <PopoverTrigger>Open popover</PopoverTrigger>
          <PopoverContent>Popover body</PopoverContent>
        </Popover>
        <HoverCard open>
          <HoverCardTrigger>Open hover card</HoverCardTrigger>
          <HoverCardContent>Hover card body</HoverCardContent>
        </HoverCard>
        <TooltipProvider>
          <Tooltip open>
            <TooltipTrigger>Hover target</TooltipTrigger>
            <TooltipContent>Tooltip body</TooltipContent>
          </Tooltip>
        </TooltipProvider>
      </div>,
    );

    expect(screen.getByText("Delete reservation")).toBeInTheDocument();
    expect(screen.getByText("Edit reservation")).toBeInTheDocument();
    expect(screen.getByText("Drawer title")).toBeInTheDocument();
    expect(screen.getByText("Sheet title")).toBeInTheDocument();
    expect(screen.getByText("Popover body")).toBeInTheDocument();
    expect(screen.getByText("Hover card body")).toBeInTheDocument();
    expect(screen.getAllByText("Tooltip body")[0]).toBeInTheDocument();
  });

  it("renders menu primitives in open state", () => {
    render(
      <div>
        <ContextMenu>
          <ContextMenuTrigger>Context target</ContextMenuTrigger>
          <ContextMenuContent forceMount>
            <ContextMenuLabel>Actions</ContextMenuLabel>
            <ContextMenuGroup>
              <ContextMenuItem>
                Open
                <ContextMenuShortcut>O</ContextMenuShortcut>
              </ContextMenuItem>
              <ContextMenuCheckboxItem checked>Checked action</ContextMenuCheckboxItem>
            </ContextMenuGroup>
            <ContextMenuRadioGroup value="daily">
              <ContextMenuRadioItem value="daily">Daily</ContextMenuRadioItem>
            </ContextMenuRadioGroup>
            <ContextMenuSub>
              <ContextMenuSubTrigger>More</ContextMenuSubTrigger>
              <ContextMenuSubContent forceMount>Nested action</ContextMenuSubContent>
            </ContextMenuSub>
            <ContextMenuSeparator />
          </ContextMenuContent>
        </ContextMenu>
        <DropdownMenu open>
          <DropdownMenuTrigger>Open menu</DropdownMenuTrigger>
          <DropdownMenuContent>
            <DropdownMenuLabel>Menu actions</DropdownMenuLabel>
            <DropdownMenuGroup>
              <DropdownMenuItem>
                Assign
                <DropdownMenuShortcut>A</DropdownMenuShortcut>
              </DropdownMenuItem>
              <DropdownMenuCheckboxItem checked>Visible</DropdownMenuCheckboxItem>
            </DropdownMenuGroup>
            <DropdownMenuRadioGroup value="confirmed">
              <DropdownMenuRadioItem value="confirmed">Confirmed</DropdownMenuRadioItem>
            </DropdownMenuRadioGroup>
            <DropdownMenuSub>
              <DropdownMenuSubTrigger>More</DropdownMenuSubTrigger>
              <DropdownMenuSubContent>Nested menu</DropdownMenuSubContent>
            </DropdownMenuSub>
            <DropdownMenuSeparator />
          </DropdownMenuContent>
        </DropdownMenu>
        <Menubar>
          <MenubarMenu>
            <MenubarTrigger>File</MenubarTrigger>
            <MenubarContent forceMount>
              <MenubarLabel>File actions</MenubarLabel>
              <MenubarGroup>
                <MenubarItem>
                  New
                  <MenubarShortcut>N</MenubarShortcut>
                </MenubarItem>
                <MenubarCheckboxItem checked>Autosave</MenubarCheckboxItem>
              </MenubarGroup>
              <MenubarRadioGroup value="compact">
                <MenubarRadioItem value="compact">Compact</MenubarRadioItem>
              </MenubarRadioGroup>
              <MenubarSub>
                <MenubarSubTrigger>More</MenubarSubTrigger>
                <MenubarSubContent forceMount>Nested file action</MenubarSubContent>
              </MenubarSub>
              <MenubarSeparator />
            </MenubarContent>
          </MenubarMenu>
        </Menubar>
      </div>,
    );

    expect(screen.getByText("Menu actions")).toBeInTheDocument();
    expect(screen.getByText("File")).toBeInTheDocument();
  });

  it("renders navigation and sidebar primitives", () => {
    render(
      <SidebarProvider defaultOpen>
        <Sidebar>
          <SidebarHeader>Admin</SidebarHeader>
          <SidebarContent>
            <SidebarGroup>
              <SidebarGroupLabel>Operations</SidebarGroupLabel>
              <SidebarGroupAction>+</SidebarGroupAction>
              <SidebarGroupContent>
                <SidebarMenu>
                  <SidebarMenuItem>
                    <SidebarMenuButton isActive>Reservations</SidebarMenuButton>
                    <SidebarMenuAction>Pin</SidebarMenuAction>
                    <SidebarMenuBadge>12</SidebarMenuBadge>
                  </SidebarMenuItem>
                  <SidebarMenuItem>
                    <SidebarMenuSkeleton showIcon />
                  </SidebarMenuItem>
                </SidebarMenu>
                <SidebarMenuSub>
                  <SidebarMenuSubItem>
                    <SidebarMenuSubButton isActive>Calendar</SidebarMenuSubButton>
                  </SidebarMenuSubItem>
                </SidebarMenuSub>
              </SidebarGroupContent>
            </SidebarGroup>
          </SidebarContent>
          <SidebarFooter>Footer</SidebarFooter>
          <SidebarRail />
          <SidebarSeparator />
          <SidebarInput placeholder="Search" />
        </Sidebar>
        <SidebarInset>
          <SidebarTrigger>Toggle</SidebarTrigger>
          <NavigationMenu>
            <NavigationMenuList>
              <NavigationMenuItem>
                <NavigationMenuTrigger>Fleet</NavigationMenuTrigger>
                <NavigationMenuContent>Fleet menu</NavigationMenuContent>
                <NavigationMenuLink href="/dashboard">Dashboard</NavigationMenuLink>
              </NavigationMenuItem>
            </NavigationMenuList>
            <NavigationMenuIndicator />
            <NavigationMenuViewport />
          </NavigationMenu>
        </SidebarInset>
      </SidebarProvider>,
    );

    expect(screen.getByText("Operations")).toBeInTheDocument();
    expect(screen.getByText("Reservations")).toBeInTheDocument();
    expect(screen.getByText("Calendar")).toBeInTheDocument();
    expect(screen.getByText("Fleet")).toBeInTheDocument();
  });

  it("renders progress and toast primitives", () => {
    render(
      <div>
        <Progress value={65} />
        <ChartContainer
          config={{ reservations: { label: "Reservations", color: "hsl(210 70% 50%)" } }}
        >
          <div>
            Chart body
            <ChartTooltipContent
              active
              label="May"
              payload={[
                {
                  dataKey: "reservations",
                  name: "reservations",
                  value: 12,
                  color: "hsl(210 70% 50%)",
                  payload: {},
                },
              ]}
            />
            <ChartLegendContent
              payload={[
                {
                  dataKey: "reservations",
                  value: "reservations",
                  color: "hsl(210 70% 50%)",
                },
              ]}
            />
          </div>
        </ChartContainer>
        <Toggle pressed>Pressed</Toggle>
        <ToggleGroup type="single" value="list">
          <ToggleGroupItem value="list">List</ToggleGroupItem>
        </ToggleGroup>
        <ScrollArea className="h-20 w-20">
          <div>Scrollable content</div>
          <ScrollBar orientation="horizontal" />
        </ScrollArea>
        <ToastProvider>
          <Toast open>
            <ToastTitle>Saved</ToastTitle>
            <ToastDescription>Reservation updated.</ToastDescription>
            <ToastAction altText="Undo">Undo</ToastAction>
          </Toast>
          <ToastViewport />
        </ToastProvider>
        <Toaster />
        <ResizablePanelGroup direction="horizontal">
          <ResizablePanel defaultSize={70}>Left panel</ResizablePanel>
          <ResizableHandle withHandle />
          <ResizablePanel defaultSize={30}>Right panel</ResizablePanel>
        </ResizablePanelGroup>
      </div>,
    );

    expect(screen.getByText("Chart body")).toBeInTheDocument();
    expect(screen.getByText("Pressed")).toBeInTheDocument();
    expect(screen.getByText("Scrollable content")).toBeInTheDocument();
    expect(screen.getByText("Saved")).toBeInTheDocument();
    expect(screen.getByText("Reservation updated.")).toBeInTheDocument();
    expect(screen.getByText("Left panel")).toBeInTheDocument();
  });

  it("renders rich prompt and media primitives", () => {
    const reelItems = [
      {
        id: "one",
        type: "image" as const,
        username: "alanya-rentacar",
        avatar: "/avatar.png",
        src: "/vehicle.jpg",
        duration: 5,
        alt: "Vehicle",
        title: "Featured vehicle",
        description: "Airport-ready sedan",
        isRead: false,
      },
    ];

    render(
      <div>
        <CircularLoader />
        <ClassicLoader />
        <PulseLoader />
        <PulseDotLoader />
        <DotsLoader />
        <TypingLoader />
        <WaveLoader />
        <BarsLoader />
        <TerminalLoader />
        <TextBlinkLoader text="Blink" />
        <TextShimmerLoader text="Shimmer" />
        <TextDotsLoader text="Text dots" />
        <PromptLoader variant="bars" text="Prompt loading" />
        <Reel data={reelItems} autoPlay={false}>
          <ReelOverlay>
            <ReelHeader>Reel header</ReelHeader>
            <ReelContent>
              {() => (
              <ReelItem>
                <ReelImage src="/vehicle.jpg" alt="Vehicle" />
              </ReelItem>
              )}
            </ReelContent>
            <ReelFooter>Reel footer</ReelFooter>
            <ReelControls>
              <ReelPreviousButton>Prev</ReelPreviousButton>
              <ReelPlayButton>Play</ReelPlayButton>
              <ReelMuteButton>Mute</ReelMuteButton>
              <ReelNextButton>Next</ReelNextButton>
            </ReelControls>
            <ReelNavigation aria-label="Go to first reel item" />
            <ReelProgress />
          </ReelOverlay>
        </Reel>
      </div>,
    );

    expect(screen.getAllByText("Loading").length).toBeGreaterThan(0);
    expect(screen.getByText("Reel header")).toBeInTheDocument();
    expect(screen.getByAltText("Vehicle")).toBeInTheDocument();
  });
});
