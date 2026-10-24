using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Provider;
using System.Windows;

namespace Studio.Windows;

// The native TabControl's mixed HWND/virtual fragment enumeration omits the final
// header in FindAll even though its reverse RawView navigation exposes it.
// Supply one coherent UIA fragment for the real headers, retaining native MSAA,
// drawing, keyboard handling and the actual hosted page controls.
public sealed class AccessibleTabControl : TabControl
{
    private TabProvider? provider;
    [DllImport("UIAutomationCore.dll",PreserveSig=false)]
    [return:MarshalAs(UnmanagedType.Interface)]
    private static extern IRawElementProviderSimple UiaProviderFromIAccessible([MarshalAs(UnmanagedType.Interface)] Accessibility.IAccessible accessible,int childId,uint flags);

    protected override void WndProc(ref Message message)
    {
        if(message.Msg==0x003D && unchecked((int)message.LParam)==-25)
        {
            provider??=new TabProvider(this);
            message.Result=AutomationInteropProvider.ReturnRawElementProvider(Handle,message.WParam,message.LParam,provider);
            return;
        }
        base.WndProc(ref message);
    }
    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        if(provider is not null&&SelectedIndex>=0&&AutomationInteropProvider.ClientsAreListening){
            var header=provider.Header(SelectedIndex);
            AutomationInteropProvider.RaiseStructureChangedEvent(provider,new StructureChangedEventArgs(StructureChangeType.ChildrenInvalidated,provider.GetRuntimeId()));
            AutomationInteropProvider.RaiseAutomationEvent(SelectionItemPatternIdentifiers.ElementSelectedEvent,header,new AutomationEventArgs(SelectionItemPatternIdentifiers.ElementSelectedEvent));
            if(Focused)AutomationInteropProvider.RaiseAutomationEvent(AutomationElementIdentifiers.AutomationFocusChangedEvent,header,new AutomationEventArgs(AutomationElementIdentifiers.AutomationFocusChangedEvent));
        }
    }
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if(provider is not null&&SelectedIndex>=0&&AutomationInteropProvider.ClientsAreListening)
            AutomationInteropProvider.RaiseAutomationEvent(AutomationElementIdentifiers.AutomationFocusChangedEvent,provider.Header(SelectedIndex),new AutomationEventArgs(AutomationElementIdentifiers.AutomationFocusChangedEvent));
    }
    private T Read<T>(Func<T> action)=>InvokeRequired?(T)Invoke(action):action();
    private void Act(Action action){if(InvokeRequired)Invoke(action);else action();}
    [ComVisible(true)]
    private sealed class TabProvider(AccessibleTabControl owner) : IRawElementProviderFragmentRoot,ISelectionProvider
    {
        private readonly Dictionary<TabPage,HeaderProvider> headers=[];
        private readonly Dictionary<TabPage,PageProvider> pages=[];
        internal PageProvider? Content()=>owner.Read(()=>{
            if(owner.SelectedTab is not { } page)return null;
            if(!pages.TryGetValue(page,out var value))pages[page]=value=new PageProvider(owner,this,page);
            return value;
        });
        internal HeaderProvider Header(int i)=>owner.Read(()=>{
            var page=owner.TabPages[i];
            if(!headers.TryGetValue(page,out var value))headers[page]=value=new HeaderProvider(owner,this,page);
            return value;
        });
        public ProviderOptions ProviderOptions=>ProviderOptions.ServerSideProvider;
        public IRawElementProviderSimple HostRawElementProvider=>owner.Read(()=>AutomationInteropProvider.HostProviderFromHandle(owner.Handle));
        public object? GetPatternProvider(int patternId)=>patternId==SelectionPatternIdentifiers.Pattern.Id?this:null;
        public object? GetPropertyValue(int propertyId)=>owner.Read<object?>(()=>propertyId switch{
            var p when p==AutomationElementIdentifiers.ControlTypeProperty.Id=>ControlType.Tab.Id,
            var p when p==AutomationElementIdentifiers.NameProperty.Id=>owner.AccessibleName??"Κύριες ενότητες",
            var p when p==AutomationElementIdentifiers.AutomationIdProperty.Id=>owner.Name,
            var p when p==AutomationElementIdentifiers.IsEnabledProperty.Id=>owner.Enabled,
            var p when p==AutomationElementIdentifiers.IsControlElementProperty.Id=>true,
            var p when p==AutomationElementIdentifiers.IsContentElementProperty.Id=>true,
            var p when p==AutomationElementIdentifiers.IsKeyboardFocusableProperty.Id=>true,
            var p when p==AutomationElementIdentifiers.HasKeyboardFocusProperty.Id=>owner.Focused,
            _=>null});
        public IRawElementProviderFragment? Navigate(NavigateDirection direction)=>owner.Read<IRawElementProviderFragment?>(()=>direction switch{
            NavigateDirection.FirstChild when owner.TabCount>0=>Header(0),
            NavigateDirection.LastChild=>Content(),
            _=>null});
        public int[] GetRuntimeId()=>owner.Read(()=>new[]{AutomationInteropProvider.AppendRuntimeId,unchecked((int)owner.Handle),0});
        public Rect BoundingRectangle=>owner.Read(()=>ScreenBounds(owner,owner.ClientRectangle));
        public IRawElementProviderSimple[]? GetEmbeddedFragmentRoots()=>null;
        public void SetFocus()=>owner.Act(()=>owner.Focus());
        public IRawElementProviderFragmentRoot FragmentRoot=>this;
        public IRawElementProviderFragment? ElementProviderFromPoint(double x,double y)=>owner.Read<IRawElementProviderFragment?>(()=>{
            for(var i=0;i<owner.TabCount;i++)if(ScreenBounds(owner,owner.GetTabRect(i)).Contains(x,y))return Header(i);
            return this;
        });
        public IRawElementProviderFragment? GetFocus()=>owner.Read<IRawElementProviderFragment?>(()=>owner.Focused&&owner.SelectedIndex>=0?Header(owner.SelectedIndex):null);
        public IRawElementProviderSimple[] GetSelection()=>owner.Read(()=>owner.SelectedIndex>=0?new IRawElementProviderSimple[]{Header(owner.SelectedIndex)}:[]);
        public bool CanSelectMultiple=>false;
        public bool IsSelectionRequired=>true;
    }
    [ComVisible(true)]
    private sealed class HeaderProvider(AccessibleTabControl owner,TabProvider root,TabPage page) : IRawElementProviderFragment,ISelectionItemProvider
    {
        public ProviderOptions ProviderOptions=>ProviderOptions.ServerSideProvider;
        public IRawElementProviderSimple? HostRawElementProvider=>null;
        public object? GetPatternProvider(int patternId)=>patternId==SelectionItemPatternIdentifiers.Pattern.Id?this:null;
        public object? GetPropertyValue(int propertyId)=>owner.Read<object?>(()=>propertyId switch{
            var p when p==AutomationElementIdentifiers.ControlTypeProperty.Id=>ControlType.TabItem.Id,
            var p when p==AutomationElementIdentifiers.NameProperty.Id=>page.Text,
            var p when p==AutomationElementIdentifiers.AutomationIdProperty.Id=>page.Name+".header",
            var p when p==AutomationElementIdentifiers.IsEnabledProperty.Id=>owner.Enabled&&page.Enabled,
            var p when p==AutomationElementIdentifiers.IsControlElementProperty.Id=>true,
            var p when p==AutomationElementIdentifiers.IsContentElementProperty.Id=>true,
            var p when p==AutomationElementIdentifiers.IsKeyboardFocusableProperty.Id=>true,
            var p when p==AutomationElementIdentifiers.HasKeyboardFocusProperty.Id=>owner.Focused&&owner.SelectedTab==page,
            var p when p==AutomationElementIdentifiers.IsOffscreenProperty.Id=>!owner.Visible,
            _=>null});
        public IRawElementProviderFragment? Navigate(NavigateDirection direction)=>owner.Read<IRawElementProviderFragment?>(()=>{
            var i=owner.TabPages.IndexOf(page);
            return direction switch{
                NavigateDirection.Parent=>root,
                NavigateDirection.NextSibling when i>=0&&i+1<owner.TabCount=>root.Header(i+1),
                NavigateDirection.NextSibling when i==owner.TabCount-1=>root.Content(),
                NavigateDirection.PreviousSibling when i>0=>root.Header(i-1),
                _=>null};
        });
        public int[] GetRuntimeId()=>owner.Read(()=>new[]{AutomationInteropProvider.AppendRuntimeId,unchecked((int)owner.Handle),page.GetHashCode()});
        public Rect BoundingRectangle=>owner.Read(()=>owner.TabPages.IndexOf(page) is var i&&i>=0?ScreenBounds(owner,owner.GetTabRect(i)):Rect.Empty);
        public IRawElementProviderSimple[]? GetEmbeddedFragmentRoots()=>null;
        public void SetFocus()=>owner.Act(()=>{owner.SelectedTab=page;owner.Focus();});
        public IRawElementProviderFragmentRoot FragmentRoot=>root;
        public bool IsSelected=>owner.Read(()=>owner.SelectedTab==page);
        public IRawElementProviderSimple SelectionContainer=>root;
        public void Select()=>owner.Act(()=>owner.SelectedTab=page);
        public void AddToSelection()=>Select();
        public void RemoveFromSelection()=>throw new InvalidOperationException("Μία καρτέλα πρέπει να παραμένει επιλεγμένη.");
    }
    [ComVisible(true)]
    private sealed class PageProvider : IRawElementProviderFragment
    {
        private readonly AccessibleTabControl owner;
        private readonly TabProvider root;
        private readonly TabPage page;
        private readonly IRawElementProviderSimple bridge;
        private readonly IRawElementProviderFragment fragment;
        internal PageProvider(AccessibleTabControl owner,TabProvider root,TabPage page)
        {
            this.owner=owner;this.root=root;this.page=page;
            bridge=UiaProviderFromIAccessible((Accessibility.IAccessible)page.AccessibilityObject,0,0);
            fragment=bridge as IRawElementProviderFragment??throw new InvalidOperationException("Η γέφυρα περιεχομένου δεν εκθέτει UIA fragment.");
        }
        public ProviderOptions ProviderOptions=>ProviderOptions.ServerSideProvider;
        public IRawElementProviderSimple HostRawElementProvider=>owner.Read(()=>AutomationInteropProvider.HostProviderFromHandle(page.Handle));
        public object? GetPatternProvider(int patternId)=>bridge.GetPatternProvider(patternId);
        public object? GetPropertyValue(int propertyId)=>owner.Read<object?>(()=>propertyId switch{
            var p when p==AutomationElementIdentifiers.ControlTypeProperty.Id=>ControlType.Pane.Id,
            var p when p==AutomationElementIdentifiers.NameProperty.Id=>page.Text,
            var p when p==AutomationElementIdentifiers.AutomationIdProperty.Id=>page.Name+".content",
            var p when p==AutomationElementIdentifiers.IsControlElementProperty.Id=>true,
            var p when p==AutomationElementIdentifiers.IsContentElementProperty.Id=>true,
            _=>bridge.GetPropertyValue(propertyId)});
        public IRawElementProviderFragment? Navigate(NavigateDirection direction)=>direction switch{
            NavigateDirection.Parent=>root,
            NavigateDirection.PreviousSibling=>owner.Read(()=>root.Header(owner.TabCount-1)),
            NavigateDirection.NextSibling=>null,
            _=>fragment.Navigate(direction)};
        public int[] GetRuntimeId()=>fragment.GetRuntimeId();
        public Rect BoundingRectangle=>owner.Read(()=>ScreenBounds(page,page.ClientRectangle));
        public IRawElementProviderSimple[]? GetEmbeddedFragmentRoots()=>null;
        public void SetFocus()=>owner.Act(()=>page.SelectNextControl(null,true,true,true,false));
        public IRawElementProviderFragmentRoot FragmentRoot=>root;
    }
    private static Rect ScreenBounds(Control owner,System.Drawing.Rectangle rectangle)
    {
        var screen=owner.RectangleToScreen(rectangle);return new Rect(screen.X,screen.Y,screen.Width,screen.Height);
    }
}
